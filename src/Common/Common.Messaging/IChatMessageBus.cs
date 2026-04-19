using ChatKnut.Data.Chat.Models;

namespace ChatKnut.Common.Messaging;

// Cross-service bus for freshly persisted chat messages. Ingestion publishes
// each ChatMessage after it commits a batch; the backend forwards them to
// GraphQL subscribers.
public interface IChatMessageBus
{
    Task PublishAsync(ChatMessage message, CancellationToken cancellationToken = default);
}

public interface IChatMessageSubscriber
{
    // Starts a long-running subscription. The handler is invoked for every
    // ChatMessage published via IChatMessageBus until cancellationToken fires.
    Task SubscribeAsync(
        Func<ChatMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken);
}
