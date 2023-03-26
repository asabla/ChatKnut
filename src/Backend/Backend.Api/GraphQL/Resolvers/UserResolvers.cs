using Data.ChatKnutDB.Repositories;
using Data.StoreObjects.Models;

namespace Backend.Api.GraphQL.Resolvers;

[ExtendObjectType(nameof(Query))]
public sealed class UserResolvers
{
    [UseOffsetPaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    [GraphQLDescription("All users registered by the system")]
    public Task<IReadOnlyList<User>> GetUsers(
        UserRepository userRepository,
        CancellationToken cancellationToken)
        => userRepository.GetUsersAsync(cancellationToken);
}