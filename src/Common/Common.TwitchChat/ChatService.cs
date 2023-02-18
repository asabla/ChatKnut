using System.Net.Sockets;

using ChatKnut.Common.TwitchChat.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatKnut.Common.TwitchChat;

public class ChatService : BackgroundService
{
    private readonly ILogger<ChatService> _logger;

    private TcpClient _tcpClient;
    private readonly string _ircAccountName;

    private StreamWriter _outputStream = null!;
    private StreamReader _inputStream = null!;

    private const string IrcHostString = "irc.chat.twitch.tv";
    private const int IrcHostPort = 6667;

    public event EventHandler<ChatMessageEventArgs> MessageReceivedEvent = null!;

    public ChatService(ILogger<ChatService> logger)
    {
        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));

        _tcpClient = new TcpClient();

        var rnd = new Random();
        _ircAccountName = $"justinfan{rnd.Next(100, 999)}";
    }

    public ChatService(ILogger<ChatService> logger, string ircAccountName)
        : this(logger)
    {
        _ircAccountName = ircAccountName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting {serviceName} with nick '{ircAccountName}'",
            nameof(ChatService),
            _ircAccountName);

        await ConnectToIrcAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_tcpClient.Connected)
                {
                    var msg = await ReadMessageAsync();
                    if (msg?.IsEmpty != false) continue;

                    if (msg.IsPing)
                    {
                        await SendPongResponseAsync();
                    }
                    else
                    {
                        _logger.LogDebug(
                            "{CreatedAt} [Channel: {Channel}] [Nick: {Sender}] - {Message}",
                            msg.CreatedAt, msg.Channel, msg.Sender, msg.Message);

                        // Raise an event when if message was received
                        OnMessageReceivedEvent(new ChatMessageEventArgs(msg));
                    }
                }
                else
                {
                    _logger.LogWarning("Is not connected, trying to reconnect");

                    await ConnectToIrcAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Twitch listener stopped unexpectedly");

                _logger.LogDebug("Waiting 5s until trying to re-connect");
                await Task.Delay(5000, stoppingToken);
            }
        }

        await Disconnect();
    }

    protected virtual void OnMessageReceivedEvent(ChatMessageEventArgs e)
    {
        EventHandler<ChatMessageEventArgs> raiseEvent = MessageReceivedEvent;

        // Event will be null if there aren't any subscribers
        if (raiseEvent is not null)
            raiseEvent(this, e);
    }

    public async Task JoinChannelAsync(string channel)
    {
        if (!channel.StartsWith("#"))
            throw new ArgumentException("Channel must start with a #", nameof(channel));

        _logger.LogInformation("Joining channel '{channel}'", channel);

        await SendMessageAsync($"JOIN {channel}");
    }

    private async Task SendMessageAsync(string message)
    {
        await _outputStream.WriteLineAsync(message);
        await _outputStream.FlushAsync();
    }

    private async Task ConnectToIrcAsync(CancellationToken cancellationToken)
    {
        // Make sure we don't have any dangling connection from earlier
        await Disconnect();

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(
            IrcHostString,
            IrcHostPort,
            cancellationToken);

        _logger.LogInformation(
            "Connected to {ircHostingString}:{ircHostPort}",
            IrcHostString,
            IrcHostPort);

        _outputStream = new StreamWriter(_tcpClient.GetStream());
        _inputStream = new StreamReader(_tcpClient.GetStream());

        // Handshake with server
        await SendMessageAsync($"NICK {_ircAccountName}");
        await SendMessageAsync("PASS SCHMOOPIIE");

        _logger.LogInformation(
            "Sent handshake with nick: '{ircAccountName}'",
            _ircAccountName);
    }

    private async Task SendPongResponseAsync()
    {
        await SendMessageAsync("PONG :tmi.twitch.tv");

        _logger.LogInformation("Sent PONG response");
    }

    private async Task<RawircMessage> ReadMessageAsync()
    {
        var message = await _inputStream.ReadLineAsync();

        if (message?.StartsWith(":tmi.twitch.tv") != false)
            return null!;

        try
        {
            return new RawircMessage(message ?? string.Empty);
        }
        catch
        {
            _logger.LogDebug(
                "Unable to parse message: {message}",
                message);

            return null!;
        }
    }

    private Task Disconnect()
    {
        _inputStream?.Close();
        _outputStream?.Close();
        _tcpClient?.Close();

        _inputStream = null!;
        _outputStream = null!;
        _tcpClient = null!;

        _logger.LogInformation(
            "Disconnected as user {ircAccountName}",
            _ircAccountName);

        return Task.CompletedTask;
    }
}