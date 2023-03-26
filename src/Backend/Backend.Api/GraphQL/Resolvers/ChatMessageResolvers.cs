using Data.ChatKnutDB.Repositories;
using Data.StoreObjects.Models;

namespace Backend.Api.GraphQL.Resolvers;

[ExtendObjectType(nameof(Query))]
public sealed class ChatMessageResolvers
{

    [UseOffsetPaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    [GraphQLDescription("All messages registered by the system")]
    public Task<IReadOnlyList<ChatMessage>> GetMessages(
        ChatMessageRepository chatMessageRepository,
        CancellationToken cancellationToken)
        => chatMessageRepository.GetChatMessagesAsync(cancellationToken);
}
