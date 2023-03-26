using Data.ChatKnutDB.Repositories;
using Data.StoreObjects.Models;

namespace Backend.Api.GraphQL.Resolvers;

[ExtendObjectType(nameof(Query))]
public sealed class ChannelResolvers
{
    [UseOffsetPaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    [GraphQLDescription("All channels registered by the system")]
    public Task<IReadOnlyList<Channel>> GetChannels(
        ChannelRepository channelRepository,
        CancellationToken cancellationToken)
        => channelRepository.GetChannelsAsync(cancellationToken);
}