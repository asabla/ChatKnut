using System.Text.Json;

using ChatKnut.Data.Chat.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ChatKnut.Data.Chat.Services;

// Lightweight projection of User used for FK lookups and live payloads. No
// navigation properties so it is safe to serialize into the distributed cache
// and cross DbContext boundaries without dragging a change tracker along.
public sealed record UserRef(Guid Id, string UserName, DateTime CreatedUtc);

public sealed record ChannelRef(Guid Id, string ChannelName, DateTime CreatedUtc);

public interface IChatRepository
{
    Task<ChatKnutDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);

    Task<UserRef> GetOrCreateUserAsync(
        ChatKnutDbContext context, string userName, CancellationToken cancellationToken = default);

    Task<ChannelRef> GetOrCreateChannelAsync(
        ChatKnutDbContext context, string channelName, CancellationToken cancellationToken = default);

    Task PromoteToCacheAsync(
        IEnumerable<UserRef> users,
        IEnumerable<ChannelRef> channels,
        CancellationToken cancellationToken = default);
}

public sealed partial class ChatRepository(
    IDbContextFactory<ChatKnutDbContext> _dbContextFactory,
    IDistributedCache _cache,
    ILogger<ChatRepository> _logger) : IChatRepository
{
    private static readonly DistributedCacheEntryOptions UserEntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
    };

    private static readonly DistributedCacheEntryOptions ChannelEntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(3),
    };

    public Task<ChatKnutDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => _dbContextFactory.CreateDbContextAsync(cancellationToken);

    public async Task<UserRef> GetOrCreateUserAsync(
        ChatKnutDbContext context,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"user:{userName}";

        var cached = await TryGetAsync<UserRef>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            LogUserCacheHit(_logger, userName);
            return cached;
        }

        LogUserCacheMiss(_logger, userName);

        var existing = await context.Users
            .AsNoTracking()
            .Where(x => x.UserName == userName)
            .Select(x => new UserRef(x.Id, x.UserName, x.CreatedUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            LogUserLoadedFromDb(_logger, userName);
            await SetAsync(cacheKey, existing, UserEntryOptions, cancellationToken);
            return existing;
        }

        LogUserCreated(_logger, userName);

        var entity = new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            CreatedUtc = DateTime.UtcNow,
        };
        await context.Users.AddAsync(entity, cancellationToken);

        // DO NOT cache yet — the SaveChanges at the end of the batch may roll
        // back. The caller is responsible for calling PromoteToCacheAsync only
        // after a successful commit.
        return new UserRef(entity.Id, entity.UserName, entity.CreatedUtc);
    }

    public async Task<ChannelRef> GetOrCreateChannelAsync(
        ChatKnutDbContext context,
        string channelName,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"channel:{channelName}";

        var cached = await TryGetAsync<ChannelRef>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            LogChannelCacheHit(_logger, channelName);
            return cached;
        }

        LogChannelCacheMiss(_logger, channelName);

        var existing = await context.Channels
            .AsNoTracking()
            .Where(x => x.ChannelName == channelName)
            .Select(x => new ChannelRef(x.Id, x.ChannelName, x.CreatedUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            LogChannelLoadedFromDb(_logger, channelName);
            await SetAsync(cacheKey, existing, ChannelEntryOptions, cancellationToken);
            return existing;
        }

        LogChannelCreated(_logger, channelName);

        var entity = new Channel
        {
            Id = Guid.NewGuid(),
            ChannelName = channelName,
            CreatedUtc = DateTime.UtcNow,
        };
        await context.Channels.AddAsync(entity, cancellationToken);

        return new ChannelRef(entity.Id, entity.ChannelName, entity.CreatedUtc);
    }

    public async Task PromoteToCacheAsync(
        IEnumerable<UserRef> users,
        IEnumerable<ChannelRef> channels,
        CancellationToken cancellationToken = default)
    {
        foreach (var user in users)
            await SetAsync($"user:{user.UserName}", user, UserEntryOptions, cancellationToken);

        foreach (var channel in channels)
            await SetAsync($"channel:{channel.ChannelName}", channel, ChannelEntryOptions, cancellationToken);
    }

    private async Task<T?> TryGetAsync<T>(string key, CancellationToken cancellationToken)
        where T : class
    {
        var bytes = await _cache.GetAsync(key, cancellationToken);
        if (bytes is null || bytes.Length == 0) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(bytes);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Corrupt cache entry for {Key}, evicting", key);
            await _cache.RemoveAsync(key, cancellationToken);
            return null;
        }
    }

    private Task SetAsync<T>(
        string key, T value, DistributedCacheEntryOptions options, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        return _cache.SetAsync(key, bytes, options, cancellationToken);
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