using Data.ChatKnutDB.Repositories;
using Data.StoreObjects.Models;

namespace Backend.Api.GraphQL;

public class Query
{
    public Book GetBook() =>
        new Book
        {
            Title = "Some Book title",
            Author = new()
            {
                Name = "Some Author Name"
            }
        };

    public Task<IReadOnlyList<Channel>> GetChannels(
        ChannelRepository channelRepository,
        CancellationToken cancellationToken)
        => channelRepository.GetChannelsAsync(cancellationToken);

    public Task<IReadOnlyList<ChatMessage>> GetMessages(
        ChatMessageRepository chatMessageRepository,
        CancellationToken cancellationToken)
        => chatMessageRepository.GetChatMessagesAsync(cancellationToken);

    public Task<IReadOnlyList<User>> GetUsers(
        UserRepository userRepository,
        CancellationToken cancellationToken)
        => userRepository.GetUsersAsync(cancellationToken);
}

public record Book
{
    public string Title { get; set; } = null!;
    public Author Author { get; set; } = null!;
}

public record Author
{
    public string Name { get; set; } = null!;
}