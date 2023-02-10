using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Models;

namespace ChatKnut.Backend.Api.GraphQL;

public class Query
{
    [UseOffsetPaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<User> GetUsers(ChatKnutDbContext context)
        => context.Users;

    [UseOffsetPaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ChatMessage> GetMessages(ChatKnutDbContext context)
        => context.ChatMessages;

    [UseOffsetPaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Channel> GetChannels(ChatKnutDbContext context)
        => context.Channels;
}