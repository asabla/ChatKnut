using ChatKnut.Common.Messaging;
using ChatKnut.Data.Chat.Models;

using HotChocolate.Subscriptions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatKnut.Backend.Api.Subscriptions;

// Bridges Redis-bus chat messages into HotChocolate's in-process topic
// router so the GraphQL Subscription<T> field can deliver them to clients.
// Runs for the lifetime of the host.
public sealed class ChatMessageBridge(
    IChatMessageSubscriber _subscriber,
    ITopicEventSender _sender,
    ILogger<ChatMessageBridge> _logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _subscriber.SubscribeAsync(async (message, ct) =>
        {
            if (string.IsNullOrWhiteSpace(message.ChannelName))
            {
                _logger.LogWarning("Dropping relay for message {MessageId} without channel", message.Id);
                return;
            }

            // Publish on both a per-channel topic (for future [Topic] routing)
            // and the subscription field name so existing clients keep working.
            await _sender.SendAsync(message.ChannelName, message, ct);
            await _sender.SendAsync(nameof(GraphQL.Subscription.ChatMessageReceived), message, ct);
        }, stoppingToken);
}