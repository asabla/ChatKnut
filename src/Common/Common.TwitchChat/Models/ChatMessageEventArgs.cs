namespace ChatKnut.Common.TwitchChat.Models;

public class ChatMessageEventArgs : EventArgs
{
    public long Tick { get; }
    public DateTimeOffset Received { get; }
    public RawircMessage Message { get; set; } = null!;

    public ChatMessageEventArgs(RawircMessage message)
    {
        Message = message;
        Received = message.CreatedAt;
        Tick = message.CreatedAt.Ticks;
    }
}