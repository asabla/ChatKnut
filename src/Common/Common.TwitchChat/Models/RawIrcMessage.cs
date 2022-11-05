namespace ChatKnut.Common.TwitchChat.Models;

public record RawIrcMessage(string RawMessage)
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsEmpty => string.IsNullOrWhiteSpace(RawMessage);
    public bool IsPing => RawMessage.StartsWith("PING");

    public string Channel => RawMessage.Split(' ')[2].TrimStart('#') ?? null!;
    public string Sender => RawMessage.Split('!')[0].TrimStart(':') ?? null!;
    public string Message => RawMessage.Split(':', 3).Length >= 3
        ? RawMessage.Split(':', 3)[2]
        : null!;
}