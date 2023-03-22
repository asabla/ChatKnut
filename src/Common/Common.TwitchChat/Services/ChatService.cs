using System.Net.Sockets;

using Common.TwitchChat.Models;

using Microsoft.Extensions.Logging;

namespace Common.TwitchChat.Services;

internal interface IChatService : IDisposable
{
    Task JoinChannelAsync(string channelName);
    Task Connect(CancellationToken cancellationToken);
    Task Disconnect();
}

internal class ChatService : IChatService
{
    private readonly ILogger<ChatService> _logger;
    private readonly string _ircAccountName;

    private readonly TcpClient _tcpClient = default!;
    private StreamReader _inputStream = null!;
    private StreamWriter _outputStream = null!;

    public event EventHandler<IrcEventArgs> MessageReceivedEvent = null!;

    public ChatService(string accountName, ILogger<ChatService> logger)
    {
        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));

        _tcpClient = new TcpClient();
        _ircAccountName = accountName;
    }

    protected virtual void OnMessageReceivedEvent(IrcEventArgs eventArgs)
    {
        EventHandler<IrcEventArgs> raiseEvent = MessageReceivedEvent;

        // If there are no subscribers event will be null
        if (raiseEvent != null)
        {
            raiseEvent(this, eventArgs);
        }
    }

    public async Task Connect(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Starting {nameof(ChatService)}");

        using (_logger.BeginScope($"Connect - {_ircAccountName}"))
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_tcpClient.Connected is false)
                {
                    _logger.LogInformation("Is not connected, trying to connect");
                    await ConnectToIrcServerAsync();
                }

                var msg = await ReadMessageAsync();

                // msg.IsEmpty will be true if it fails to parse text content
                // of a received message
                if (msg is null || msg.IsEmpty is true)
                    continue;

                if (msg.IsPing is true)
                {
                    await SendPongResponseAsync();
                    continue;
                }

                // Normal message received, log and raise event
                _logger.LogDebug(
                    "{CreatedAt} [Channel: {Channel}] [Nick: {Sender}] - {Message}",
                    msg.CreatedAt, msg.Channel, msg.Sender, msg.Message);

                OnMessageReceivedEvent(new IrcEventArgs(msg));
            }
        }

        _logger.LogInformation("Disconnect signal received from cancellation token");

        // If current call has been canceled, let's call on disconnect
        // and make sure we've disposed of everything.
        _ = Disconnect();
    }

    /// <summary>
    /// Tries to disconnect and dispose all active connections
    /// </summary>
    /// <exception cref="member">
    /// If it fails to disconnect an exception with be thrown
    /// </exception>
    /// <returns>Will return true if successful</returns>
    public Task Disconnect()
    {
        _logger.LogInformation("Disconnecting");

        try
        {
            Dispose();
        }
        catch
        {
            throw;
        }

        return Task.FromResult(0);
    }

    public async Task JoinChannelAsync(string channelName)
    {
        if (!channelName.StartsWith("#"))
            throw new ArgumentException("Channel must start with a #", nameof(channelName));

        _logger.LogInformation("Joining channel '{channel}'", channelName);

        await SendStringMessageAsync($"JOIN {channelName}");
    }

    public void Dispose()
    {
        _inputStream?.Close();
        _inputStream?.Dispose();

        _outputStream?.Close();
        _outputStream?.Dispose();

        _tcpClient.Close();
        _tcpClient.Dispose();
    }

    #region Private methods

    private async Task ConnectToIrcServerAsync()
    {
        // TODO: move host name + port somewhere else
        await _tcpClient.ConnectAsync("irc.chat.twitch.tv", 6667);

        _outputStream = new StreamWriter(_tcpClient.GetStream());
        _inputStream = new StreamReader(_tcpClient.GetStream());

        await SendStringMessageAsync($"NICK {_ircAccountName}");
        await SendStringMessageAsync("PASS SCHMOOPIIE");
    }

    private async Task<RawIrcMessage> ReadMessageAsync()
    {
        var message = await _inputStream.ReadLineAsync();

        if (message?.StartsWith(":tmi.twitch.tv") is not true)
            return null!;

        try
        {
            return new RawIrcMessage(message ?? string.Empty);
        }
        catch
        {
            return null!;
        }
    }

    private async Task SendPongResponseAsync()
    {
        _logger.LogInformation("Sending PONG response");

        await SendStringMessageAsync("PONG :tmi.twitch.tv");
    }

    private async Task SendStringMessageAsync(string message)
    {
        // TODO: Check if WriteLineAsync works better or at all
        await _outputStream.WriteAsync($"{message}\r\n");
        await _outputStream.FlushAsync();
    }

    #endregion End of Private methods
}