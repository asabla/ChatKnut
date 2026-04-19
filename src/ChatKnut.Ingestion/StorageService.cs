using System.Threading.Channels;

using ChatKnut.Ingestion.Models;

namespace ChatKnut.Ingestion;

public interface IStorageService
{
    int Count { get; }

    // Try to enqueue without blocking. Returns false if the queue is full,
    // which is load-shedding — the message is dropped rather than letting
    // the queue grow unboundedly while the consumer falls behind.
    bool TryEnqueue(RawIrcMessage message);

    ChannelReader<RawIrcMessage> Reader { get; }
}

public sealed class StorageService : IStorageService
{
    private readonly Channel<RawIrcMessage> _channel;
    private int _count;

    public StorageService()
    {
        _channel = Channel.CreateBounded<RawIrcMessage>(new BoundedChannelOptions(capacity: 10_000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public int Count => Volatile.Read(ref _count);

    public bool TryEnqueue(RawIrcMessage message)
    {
        if (!_channel.Writer.TryWrite(message)) return false;
        Interlocked.Increment(ref _count);
        return true;
    }

    public ChannelReader<RawIrcMessage> Reader => new TrackingReader(_channel.Reader, this);

    // Wraps the underlying reader so we can keep our own approximate count for
    // the observable gauge; Channel<T> does not expose a cheap Count.
    private sealed class TrackingReader(ChannelReader<RawIrcMessage> _inner, StorageService _owner)
        : ChannelReader<RawIrcMessage>
    {
        public override bool TryRead(out RawIrcMessage item)
        {
            if (!_inner.TryRead(out item!)) return false;
            Interlocked.Decrement(ref _owner._count);
            return true;
        }

        public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
            => _inner.WaitToReadAsync(cancellationToken);

        public override bool CanCount => _inner.CanCount;
        public override int Count => _inner.Count;
        public override bool CanPeek => _inner.CanPeek;
        public override bool TryPeek(out RawIrcMessage item) => _inner.TryPeek(out item!);
    }
}
