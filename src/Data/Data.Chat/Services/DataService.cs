using ChatKnut.Data.Chat.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ChatKnut.Data.Chat.Services;

public interface IQueueService
{
    EntityEntry<ChatMessage>? InsertMessage(ChatMessage message);
    Task<ChatKnutDbContext> CreateDbContext();
    Task<User> GetOrCreateUserAsync(string userName);
    Task<Channel> GetOrCreateChannelAsync(string channelName);
}

public partial class QueueService(
    IDbContextFactory<ChatKnutDbContext> _dbContextFactory,
    IMemoryCache _memoryCache,
    ILogger<QueueService> _logger
    ) : IQueueService
{
    private ChatKnutDbContext _dbContext = null!;

    public async Task<ChatKnutDbContext> CreateDbContext()
    {
        _dbContext = await _dbContextFactory.CreateDbContextAsync();

        return _dbContext;
    }

    public async Task<User> GetOrCreateUserAsync(string userName)
    {
        if (_memoryCache.TryGetValue($"user_{userName}", out object? value)
            && value is User cacheUser)
        {
            LogUserCacheHit(_logger, userName);
            return cacheUser;
        }
        else
        {
            LogUserCacheMiss(_logger, userName);

            User resultUser = null!;
            if (!_dbContext.Users.Any(x => x.UserName.Equals(userName)))
            {
                LogUserCreated(_logger, userName);
                var result = await _dbContext.Users.AddAsync(new()
                {
                    Id = Guid.NewGuid(),
                    UserName = userName,
                    CreatedUtc = DateTime.UtcNow
                });

                resultUser = result.Entity;
            }
            else
            {
                LogUserLoadedFromDb(_logger, userName);

                resultUser = await _dbContext.Users
                    .Where(x => x.UserName.Equals(userName))
                    .FirstOrDefaultAsync() ?? null!;
            }

            _memoryCache.Set($"user_{userName}", resultUser, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

            return resultUser;
        }
    }

    public async Task<Channel> GetOrCreateChannelAsync(string channelName)
    {
        if (_memoryCache.TryGetValue($"channel_{channelName}", out object? value)
            && value is Channel cacheChannel)
        {
            LogChannelCacheHit(_logger, channelName);
            return cacheChannel;
        }
        else
        {
            LogChannelCacheMiss(_logger, channelName);

            Channel resultChannel = null!;
            if (!_dbContext.Channels.Any(x => x.ChannelName.Equals(channelName)))
            {
                LogChannelCreated(_logger, channelName);
                var result = await _dbContext.Channels.AddAsync(new()
                {
                    Id = Guid.NewGuid(),
                    ChannelName = channelName,
                    CreatedUtc = DateTime.UtcNow
                });

                resultChannel = result.Entity;
            }
            else
            {
                LogChannelLoadedFromDb(_logger, channelName);

                resultChannel = await _dbContext.Channels
                    .Where(x => x.ChannelName.Equals(channelName))
                    .FirstOrDefaultAsync() ?? null!;
            }

            _memoryCache.Set($"channel_{channelName}", resultChannel, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(3)
            });

            return resultChannel;
        }
    }

    public EntityEntry<ChatMessage>? InsertMessage(ChatMessage message)
    {
        return _dbContext.ChatMessages.Add(message);
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "Found user {UserName} in cache")]
    private static partial void LogUserCacheHit(ILogger logger, string userName);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "User {UserName} was not found in cache")]
    private static partial void LogUserCacheMiss(ILogger logger, string userName);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Debug, Message = "User {UserName} was not found in Db, creating user")]
    private static partial void LogUserCreated(ILogger logger, string userName);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Debug, Message = "User {UserName} found in Db")]
    private static partial void LogUserLoadedFromDb(ILogger logger, string userName);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Debug, Message = "Found channel {ChannelName} in cache")]
    private static partial void LogChannelCacheHit(ILogger logger, string channelName);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Debug, Message = "Channel {ChannelName} was not found in cache")]
    private static partial void LogChannelCacheMiss(ILogger logger, string channelName);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Debug, Message = "Channel {ChannelName} was not found in Db, creating channel")]
    private static partial void LogChannelCreated(ILogger logger, string channelName);

    [LoggerMessage(EventId = 2013, Level = LogLevel.Debug, Message = "Channel {ChannelName} found in Db")]
    private static partial void LogChannelLoadedFromDb(ILogger logger, string channelName);
}