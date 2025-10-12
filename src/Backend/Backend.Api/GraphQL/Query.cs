using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Models;

using Microsoft.EntityFrameworkCore;

namespace ChatKnut.Backend.Api.GraphQL;

public class Query
{
    [UsePaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<User> GetUsers(ChatKnutDbContext context)
        => context.Users.AsNoTracking();

    [UsePaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ChatMessage> GetMessages(ChatKnutDbContext context)
        => context.ChatMessages.AsNoTracking();

    [UsePaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Channel> GetChannels(ChatKnutDbContext context)
        => context.Channels.AsNoTracking();
}