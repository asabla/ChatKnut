using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Models;

namespace ChatKnut.Backend.Api.GraphQL;

public class Query
{
    [UseOffsetPaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<User> GetUsers([ScopedService] ChatKnutDbContext context)
        => context.Users;

    [UseOffsetPaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ChatMessage> GetMessages([ScopedService] ChatKnutDbContext context)
        => context.ChatMessages;

    [UseOffsetPaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Channel> GetChannels([ScopedService] ChatKnutDbContext context)
        => context.Channels;
}