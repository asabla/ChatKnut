namespace ChatKnut.Data.Chat.Models;

public class ChatMessage
{
    public Guid Id { get; set; }

    public string? ChannelName { get; set; }

    public string? Message { get; set; }

    public DateTime CreatedUtc { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid ChannelId { get; set; }
    public Channel? Channel { get; set; }
}