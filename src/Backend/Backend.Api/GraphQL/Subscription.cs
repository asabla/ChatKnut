using ChatKnut.Data.Chat.Models;

namespace ChatKnut.Backend.Api.GraphQL;

public class Subscription
{
    [Subscribe]
    public ChatMessage ChatMessageReceived(
        [Topic] string channelName,
        [EventMessage] ChatMessage message)
        => message;
}