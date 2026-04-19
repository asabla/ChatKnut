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

public class QueueService(
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
            _logger.LogDebug("Found user {UserName} in cache", userName);
            return cacheUser;
        }
        else
        {
            _logger.LogDebug("User {UserName} was not found in cache", userName);

            User resultUser = null!;
            if (!_dbContext.Users.Any(x => x.UserName.Equals(userName)))
            {
                _logger.LogDebug("User {UserName} was not found in Db, creating user", userName);
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
                _logger.LogDebug("User {UserName} found in Db", userName);

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
            _logger.LogDebug("Found channel {ChannelName} in cache", channelName);
            return cacheChannel;
        }
        else
        {
            _logger.LogDebug("Channel {ChannelName} was not found in cache", channelName);

            Channel resultChannel = null!;
            if (!_dbContext.Channels.Any(x => x.ChannelName.Equals(channelName)))
            {
                _logger.LogDebug("Channel {ChannelName} was not found in Db, creating channel", channelName);
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
                _logger.LogDebug("Channel {ChannelName} found in Db", channelName);

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
}