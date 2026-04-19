using System.Text.Json;
using System.Threading.Channels;

using ChatKnut.Data.Chat.Models;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace ChatKnut.Common.Messaging;

internal static class RedisChannels
{
    public const string ChatMessages = "chatknut:messages";
    public const string JoinCommand = "chatknut:commands:join";
}

internal sealed class RedisChatMessageBus(
    IConnectionMultiplexer _multiplexer,
    ILogger<RedisChatMessageBus> _logger) : IChatMessageBus, IChatMessageSubscriber
{
    public async Task PublishAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        await _multiplexer.GetSubscriber()
            .PublishAsync(RedisChannel.Literal(RedisChannels.ChatMessages), payload);
    }

    public async Task SubscribeAsync(
        Func<ChatMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var subscriber = _multiplexer.GetSubscriber();
        var queue = await subscriber.SubscribeAsync(RedisChannel.Literal(RedisChannels.ChatMessages));

        using var _ = cancellationToken.Register(() => queue.Unsubscribe());

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ChannelMessage entry;
                try
                {
                    entry = await queue.ReadAsync(cancellationToken);
                }
                catch (ChannelClosedException)
                {
                    break;
                }

                ChatMessage? message;
                try
                {
                    message = JsonSerializer.Deserialize<ChatMessage>((byte[])entry.Message!);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Dropped malformed ChatMessage payload from Redis");
                    continue;
                }

                if (message is null) continue;

                try
                {
                    await handler(message, cancellationToken);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "ChatMessage subscription handler threw; continuing");
                }
            }
        }
        finally
        {
            await queue.UnsubscribeAsync();
        }
    }
}

internal sealed class RedisJoinChannelBus(
    IConnectionMultiplexer _multiplexer,
    ILogger<RedisJoinChannelBus> _logger) : IJoinChannelBus, IJoinChannelSubscriber
{
    public Task PublishJoinAsync(string channelName, CancellationToken cancellationToken = default)
        => _multiplexer.GetSubscriber()
            .PublishAsync(RedisChannel.Literal(RedisChannels.JoinCommand), channelName);

    public async Task SubscribeAsync(
        Func<string, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var subscriber = _multiplexer.GetSubscriber();
        var queue = await subscriber.SubscribeAsync(RedisChannel.Literal(RedisChannels.JoinCommand));

        using var _ = cancellationToken.Register(() => queue.Unsubscribe());

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ChannelMessage entry;
                try
                {
                    entry = await queue.ReadAsync(cancellationToken);
                }
                catch (ChannelClosedException)
                {
                    break;
                }

                var channel = (string?)entry.Message;
                if (string.IsNullOrWhiteSpace(channel)) continue;

                try
                {
                    await handler(channel, cancellationToken);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Join handler threw for {Channel}; continuing", channel);
                }
            }
        }
        finally
        {
            await queue.UnsubscribeAsync();
        }
    }
}
