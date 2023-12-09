using System.Net.Sockets;

using ChatKnut.Common.TwitchChat.Models;

using HotChocolate.Subscriptions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatKnut.Common.TwitchChat;

public class ChatService : BackgroundService
{
    private readonly IStorageService _storageService;
    private readonly ILogger<ChatService> _logger;
    private readonly ITopicEventSender _eventSender;

    private TcpClient _tcpClient;
    private readonly string _ircAccountname;

    private StreamWriter _outputStream = null!;
    private StreamReader _inputStream = null!;

    public ChatService(
        IStorageService storageService,
        ILogger<ChatService> logger,
        ITopicEventSender eventSender)
    {
        _storageService = storageService ??
            throw new ArgumentNullException(nameof(storageService));

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));

        _eventSender = eventSender ??
            throw new ArgumentNullException(nameof(eventSender));

        _tcpClient = new TcpClient();

        var rnd = new Random();
        _ircAccountname = $"justinfan{rnd.Next(100, 999)}";
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.BeginScope($"[{nameof(ChatService)}]");
        _logger.LogInformation($"Starting {nameof(ChatService)}");

        while (cancellationToken.IsCancellationRequested is false)
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

                        HandleMessage(msg);
                    }
                }
                else
                {
                    _logger.LogInformation("Is not connected, trying to connect");

                    await ConnectToIrcAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Twitch listener crashed");

                _logger.LogDebug("Waiting 5s until trying to re-connect");
                await Task.Delay(5000, cancellationToken);
            }
        }

        _logger.LogInformation($"Shutting down {nameof(ChatService)}...");

        await Disconnect();
    }

    public async Task JoinChannelAsync(string channel)
    {
        if (!channel.StartsWith("#"))
            throw new ArgumentException("Channel must start with #", nameof(channel));

        _logger.LogInformation("Joinging channel '{channel}'", channel);

        await SendStringMessageAsync($"JOIN {channel}");
    }

    #region Private methods

    private void HandleMessage(RawIrcMessage msg)
    {
        if (string.IsNullOrWhiteSpace(msg.Channel)
            || string.IsNullOrWhiteSpace(msg.Sender)
            || string.IsNullOrWhiteSpace(msg.Message))
        {
            _logger.LogWarning("Message does not contain enough information");
            return;
        }

        if (msg.Sender.StartsWith("justinfan"))
        {
            _logger.LogInformation(
                "System accounts ('{userName}') will not be logged",
                msg.Sender);

            return;
        }

        // Put message on queue
        _storageService.AddToQueue(msg);
    }

    private async Task ConnectToIrcAsync(CancellationToken token)
    {
        _tcpClient.Dispose();

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync("irc.chat.twitch.tv", 6667);

        _outputStream = new StreamWriter(_tcpClient.GetStream());
        _inputStream = new StreamReader(_tcpClient.GetStream());

        await SendStringMessageAsync($"NICK {_ircAccountname}");
        await SendStringMessageAsync("PASS SCHMOOPIIE");
    }

    private Task Disconnect()
    {
        _inputStream.Close();
        _outputStream.Close();
        _tcpClient.Close();

        return Task.CompletedTask;
    }

    private async Task SendPongResponseAsync()
    {
        _logger.LogInformation("Sending PONG message");

        await SendStringMessageAsync("PONG :tmi.twitch.tv");
    }

    private async Task<RawIrcMessage> ReadMessageAsync()
    {
        var message = await _inputStream.ReadLineAsync();

        if (message?.StartsWith(":tmi.twitch.tv") != false)
            return null!;

        try
        {
            return new RawIrcMessage(message ?? string.Empty);
        }
        catch
        {
            _logger.LogWarning($"Unable to parse message: {message}");
            return null!;
        }
    }

    private async Task SendStringMessageAsync(string message)
    {
        await _outputStream.WriteAsync($"{message}\r\n");
        await _outputStream.FlushAsync();
    }

    #endregion
}