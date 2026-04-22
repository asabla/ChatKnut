using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;

using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Services;
using ChatKnut.Ingestion.Models;
using ChatKnut.Ingestion.Telemetry;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatKnut.Ingestion;

public sealed partial class ChatService : BackgroundService
{
    // How long to wait for a line from Twitch before treating the socket as
    // stalled. Twitch sends a PING at least every ~5 minutes, so 6 minutes of
    // silence is clearly wrong.
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromMinutes(6);

    // Exponential backoff bounds for reconnect loop.
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(2);

    private readonly IStorageService _storage;
    private readonly IChatRepository _repository;
    private readonly ILogger<ChatService> _logger;

    private readonly string _ircAccountname;

    // Channels we've asked to JOIN, so we can re-JOIN them after a reconnect.
    // Set rather than list so repeated joins don't double-up.
    private readonly ConcurrentDictionary<string, byte> _joinedChannels = new(StringComparer.OrdinalIgnoreCase);

    private TcpClient? _tcpClient;
    private StreamReader? _inputStream;
    private StreamWriter? _outputStream;

    public ChatService(
        IStorageService storage,
        IChatRepository repository,
        ILogger<ChatService> logger)
    {
        _storage = storage;
        _repository = repository;
        _logger = logger;

        var rnd = Random.Shared;
        _ircAccountname = $"justinfan{rnd.Next(100, 999)}";
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Service"] = nameof(ChatService),
            ["IrcAccount"] = _ircAccountname,
        });
        _logger.LogInformation("Starting {Service}", nameof(ChatService));

        var backoff = MinBackoff;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(cancellationToken);
                await JoinAutoJoinChannelsAsync(cancellationToken);
                await ReadLoopAsync(cancellationToken);

                // Clean exit from ReadLoopAsync means the outer token was
                // cancelled; loop will terminate.
                backoff = MinBackoff;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IRC connection crashed; reconnecting in {Backoff}", backoff);
            }

            await CloseConnectionAsync();

            try
            {
                await Task.Delay(WithJitter(backoff), cancellationToken);
            }
            catch (OperationCanceledException) { break; }

            backoff = TimeSpan.FromMilliseconds(Math.Min(backoff.TotalMilliseconds * 2, MaxBackoff.TotalMilliseconds));
        }

        _logger.LogInformation("Shutting down {Service}", nameof(ChatService));

        await CloseConnectionAsync();
    }

    public async Task JoinChannelAsync(string channel)
    {
        if (!channel.StartsWith('#'))
            throw new ArgumentException("Channel must start with #", nameof(channel));

        _joinedChannels[channel] = 0;

        if (_outputStream is not null)
        {
            _logger.LogInformation("Joining channel {Channel}", channel);
            await SendStringMessageAsync($"JOIN {channel}");
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await CloseConnectionAsync();

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync("irc.chat.twitch.tv", 6667, cancellationToken);

        var stream = _tcpClient.GetStream();
        // Leave the stream open so the reader and writer don't close it on each other.
        _inputStream = new StreamReader(stream, leaveOpen: true);
        _outputStream = new StreamWriter(stream, leaveOpen: true);

        await SendStringMessageAsync($"NICK {_ircAccountname}");
        await SendStringMessageAsync("PASS SCHMOOPIIE");

        _logger.LogInformation("Connected to Twitch IRC as {IrcAccount}", _ircAccountname);
    }

    private async Task JoinAutoJoinChannelsAsync(CancellationToken cancellationToken)
    {
        await using var context = await _repository.CreateDbContextAsync(cancellationToken);

        var autoJoin = await context.Channels
            .AsNoTracking()
            .Where(c => c.AutoJoin)
            .Select(c => c.ChannelName)
            .ToListAsync(cancellationToken);

        foreach (var name in autoJoin)
            await JoinChannelAsync($"#{name}");

        // Re-JOIN any channels we had been in before a reconnect.
        foreach (var name in _joinedChannels.Keys)
            await SendStringMessageAsync($"JOIN {name}");

        _logger.LogInformation(
            "Joined {AutoJoinCount} auto-join channels, re-joined {ReJoinCount} sticky channels",
            autoJoin.Count, _joinedChannels.Count);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ReadTimeout);

            string? line;
            try
            {
                line = await _inputStream!.ReadLineAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("No IRC traffic for {Timeout}; treating as stall and reconnecting", ReadTimeout);
                return;
            }

            if (line is null)
            {
                _logger.LogWarning("IRC stream ended; reconnecting");
                return;
            }

            if (line.StartsWith(":tmi.twitch.tv"))
                continue;

            RawIrcMessage? msg;
            try
            {
                msg = new RawIrcMessage(line);
            }
            catch
            {
                ChatTelemetry.MessagesDropped.Add(
                    1, new KeyValuePair<string, object?>("reason", "parse_error"));
                LogUnparseableMessage(_logger, line);
                continue;
            }

            if (msg.IsEmpty) continue;

            if (msg.IsPing)
            {
                await SendStringMessageAsync("PONG :tmi.twitch.tv");
                continue;
            }

            LogIncomingMessage(_logger, msg.CreatedAt, msg.Channel, msg.Sender, msg.Message);
            HandleMessage(msg);
        }
    }

    private void HandleMessage(RawIrcMessage msg)
    {
        using var activity = ChatTelemetry.ActivitySource.StartActivity(
            "chatservice.handle_message", ActivityKind.Consumer);

        if (string.IsNullOrWhiteSpace(msg.Channel)
            || string.IsNullOrWhiteSpace(msg.Sender)
            || string.IsNullOrWhiteSpace(msg.Message))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "incomplete message");
            ChatTelemetry.MessagesDropped.Add(
                1, new KeyValuePair<string, object?>("reason", "incomplete"));
            LogIncompleteMessage(_logger);
            return;
        }

        activity?.SetTag("twitch.channel", msg.Channel);
        activity?.SetTag("twitch.sender", msg.Sender);

        if (msg.Sender.StartsWith("justinfan"))
        {
            activity?.SetTag("twitch.dropped_reason", "system_account");
            ChatTelemetry.MessagesDropped.Add(
                1,
                new KeyValuePair<string, object?>("reason", "system_account"),
                new KeyValuePair<string, object?>("twitch.channel", msg.Channel));
            LogSystemAccountSkipped(_logger, msg.Sender);
            return;
        }

        if (!_storage.TryEnqueue(msg))
        {
            activity?.SetTag("twitch.dropped_reason", "queue_full");
            ChatTelemetry.MessagesDropped.Add(
                1,
                new KeyValuePair<string, object?>("reason", "queue_full"),
                new KeyValuePair<string, object?>("twitch.channel", msg.Channel));
            LogQueueFull(_logger, msg.Channel);
            return;
        }

        ChatTelemetry.MessagesReceived.Add(
            1, new KeyValuePair<string, object?>("twitch.channel", msg.Channel));
    }

    private async Task SendStringMessageAsync(string message)
    {
        if (_outputStream is null) return;
        await _outputStream.WriteAsync($"{message}\r\n");
        await _outputStream.FlushAsync();
    }

    private async Task CloseConnectionAsync()
    {
        // Deterministic cleanup order: flush/close writer, dispose reader,
        // close client. The original code leaked StreamReader and
        // StreamWriter on each reconnect by only disposing the TcpClient.
        try
        {
            if (_outputStream is not null)
                await _outputStream.DisposeAsync();
        }
        catch { /* best-effort */ }

        try
        {
            _inputStream?.Dispose();
        }
        catch { /* best-effort */ }

        try
        {
            _tcpClient?.Dispose();
        }
        catch { /* best-effort */ }

        _outputStream = null;
        _inputStream = null;
        _tcpClient = null;
    }

    private static TimeSpan WithJitter(TimeSpan baseDelay)
    {
        var jitter = Random.Shared.NextDouble() * 0.3 + 0.85;
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * jitter);
    }

    #region High-volume log methods (source-generated)

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "{CreatedAt} [Channel: {Channel}] [Nick: {Sender}] - {Message}")]
    private static partial void LogIncomingMessage(
        ILogger logger, DateTimeOffset createdAt, string channel, string sender, string message);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Message does not contain enough information")]
    private static partial void LogIncompleteMessage(ILogger logger);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "System accounts ({UserName}) will not be logged")]
    private static partial void LogSystemAccountSkipped(ILogger logger, string userName);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Unable to parse message: {RawMessage}")]
    private static partial void LogUnparseableMessage(ILogger logger, string? rawMessage);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "Ingest queue full, dropping message from {Channel}")]
    private static partial void LogQueueFull(ILogger logger, string channel);

    #endregion
}