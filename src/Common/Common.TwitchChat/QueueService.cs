using System.Collections.Concurrent;

using ChatKnut.Common.TwitchChat.Models;

namespace ChatKnut.Common.TwitchChat;

public interface IStorageService
{
    void AddToQueue(RawIrcMessage message);
    bool IsEmpty();
    bool TryPeek(out RawIrcMessage? message);
    bool TryTake(out RawIrcMessage? message);
}

public class StorageService : IStorageService
{
    private readonly ConcurrentBag<RawIrcMessage> _messageQueue = new();

    public void AddToQueue(RawIrcMessage message)
        => _messageQueue.Add(message);

    public bool IsEmpty()
        => _messageQueue.IsEmpty;

    public bool TryPeek(out RawIrcMessage? message)
        => _messageQueue.TryPeek(out message);

    public bool TryTake(out RawIrcMessage? message)
        => _messageQueue.TryTake(out message);
}