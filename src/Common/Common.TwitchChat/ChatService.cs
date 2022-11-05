using System.Net.Sockets;

using ChatKnut.Common.TwitchChat.Models;
using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatKnut.Common.TwitchChat;

public class ChatService : BackgroundService
{
    private readonly ILogger<ChatService> _logger;
    private readonly IDbContextFactory<ChatKnutDbContext> _dbContextFactory;
    private readonly IMemoryCache _memoryCache;

    private TcpClient _tcpClient;
    private readonly string _ircAccountname;

    private StreamWriter _outputStream = null!;
    private StreamReader _inputStream = null!;

    public ChatService(
        ILogger<ChatService> logger,
        IDbContextFactory<ChatKnutDbContext> dbContextFactory,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _dbContextFactory = dbContextFactory ??
            throw new ArgumentNullException(nameof(dbContextFactory));

        _tcpClient = new TcpClient();

        var rnd = new Random();
        _ircAccountname = $"justinfan{rnd.Next(100, 999)}";
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Starting {nameof(ChatService)}");

        while(!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_tcpClient.Connected)
                {
                    var msg = await ReadMessageAsync();
                    if (msg is null || msg.IsEmpty) continue;

                    if (msg.IsPing)
                    {
                        await SendPongResponseAsync();
                    }
                    else
                    {
                        _logger.LogDebug(
                            "{CreatedAt} [Channel: {Channel}] [Nick: {Sender}] - {Message}",
                            msg.CreatedAt, msg.Channel, msg.Sender, msg.Message);

                        await InsertMessage(msg, cancellationToken);
                    }
                }
                else
                {
                    _logger.LogInformation("Is not connected, trying to connect");

                    await ConnectToIrcAsync(cancellationToken);
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Twitch listener crashed");

                _logger.LogDebug("Waiting 5s until trying to re-connect");
                await Task.Delay(5000, cancellationToken);
            }
        }

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

    private async Task<User> GetOrCreateUser(
        string userName,
        ChatKnutDbContext context,
        CancellationToken token)
    {
        if (_memoryCache.TryGetValue($"user_{userName}", out object? value)
            && value is User cacheUser)
        {
            _logger.LogDebug("Found user '{userName}' in cache", userName);
            return cacheUser;
        }
        else
        {
            _logger.LogDebug("User '{userName}' was not found in cache", userName);

            User resultUser = null!;
            if (!context.Users.Any(x => x.UserName.Equals(userName)))
            {
                _logger.LogDebug("User '{userName}' was not found in Db, creating user", userName);
                var result = await context.Users.AddAsync(new()
                {
                    Id = Guid.NewGuid(),
                    UserName = userName,
                    CreatedUtc = DateTime.UtcNow
                }, token);

                await context.SaveChangesAsync(cancellationToken: token);

                resultUser = result.Entity;
            }
            else
            {
                _logger.LogDebug("User '{userName}' found in Db", userName);

                resultUser = await context.Users
                    .Where(x => x.UserName.Equals(userName))
                    .FirstOrDefaultAsync(cancellationToken: token) ?? null!;
            }

            _memoryCache.Set($"user_{userName}", resultUser, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

            return resultUser;
        }
    }

    private async Task<Channel> GetOrCreateChannel(
        string channelName,
        ChatKnutDbContext context,
        CancellationToken token)
    {
        if (_memoryCache.TryGetValue($"channel_{channelName}", out object? value)
            && value is Channel cacheChannel)
        {
            _logger.LogDebug("Found channel '{channelName}' in cache", channelName);
            return cacheChannel;
        }
        else
        {
            _logger.LogDebug("Channel '{channelName}' was not found in cache", channelName);

            Channel resultChannel = null!;
            if (!context.Channels.Any(x => x.ChannelName.Equals(channelName)))
            {
                _logger.LogDebug("Channel '{channelName}' was not found in Db, creating channel", channelName);
                var result = await context.Channels.AddAsync(new()
                {
                    Id = Guid.NewGuid(),
                    ChannelName = channelName,
                    CreatedUtc = DateTime.UtcNow
                }, cancellationToken: token);

                await context.SaveChangesAsync(cancellationToken: token);

                resultChannel = result.Entity;
            }
            else
            {
                _logger.LogDebug("Channel '{channelName}' found in Db", channelName);

                resultChannel = await context.Channels
                    .Where(x => x.ChannelName.Equals(channelName))
                    .FirstOrDefaultAsync(cancellationToken: token) ?? null!;
            }

            _memoryCache.Set($"channel_{channelName}", resultChannel, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(3)
            });

            return resultChannel;
        }
    }

    private async Task InsertMessage(
        RawIrcMessage msg,
        CancellationToken cancellationToken)
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

        await using ChatKnutDbContext context
            = _dbContextFactory.CreateDbContext();

        var chatUser = await GetOrCreateUser(msg.Sender, context, cancellationToken);
        if (chatUser is null)
        {
            _logger.LogWarning("Unable to GetOrCreateUser '{userName}'", msg.Sender);
            return;
        }

        var chatChannel = await GetOrCreateChannel(msg.Channel, context, cancellationToken);
        if (chatChannel is null)
        {
            _logger.LogWarning("Unable to GetOrCreateChannel '{channelName}'", msg.Channel);
            return;
        }

        Guid userId = chatUser.Id;
        Guid? channelId = chatChannel.Id;

        _ = await context.ChatMessages.AddAsync(new()
        {
            Id = Guid.NewGuid(),
            ChannelName = msg.Channel,
            CreatedUtc = DateTime.UtcNow,
            Message = msg.Message,
            UserId = userId,
            ChannelId = channelId ?? Guid.Empty
        }, cancellationToken);

        _ = await context.SaveChangesAsync(cancellationToken);
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

        await using ChatKnutDbContext context
            = _dbContextFactory.CreateDbContext();

        var channels = await context.Channels
            .Select(x => x.ChannelName.ToLowerInvariant())
            .ToListAsync(cancellationToken: token) ?? new List<string>();

        _logger.LogInformation(
            "Found {channels.Count} number of channels to join",
            channels.Count);

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 5
        };

        foreach (var chan in channels)
        {
            await JoinChannelAsync($"#{chan}");
            await Task.Delay(100, token);
        }
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

        if (message is null || message.StartsWith(":tmi.twitch.tv"))
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

    private async Task SendStringMessageAsync(string message)
    {
        await _outputStream.WriteAsync($"{message}\r\n");
        await _outputStream.FlushAsync();
    }

    #endregion
}