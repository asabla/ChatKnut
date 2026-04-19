using System.Diagnostics;

using ChatKnut.Data.Chat.Models;
using ChatKnut.Data.Chat.Services;
using ChatKnut.Ingestion.Models;
using ChatKnut.Ingestion.Telemetry;

using HotChocolate.Subscriptions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatKnut.Ingestion;

public sealed class DataBufferService(
    IStorageService _storage,
    IChatRepository _repository,
    ITopicEventSender _eventSender,
    ILogger<DataBufferService> _logger) : BackgroundService
{
    private static readonly TimeSpan MaxBufferWindow = TimeSpan.FromMilliseconds(500);
    private const int MaxBatchSize = 1_000;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Service"] = nameof(DataBufferService),
        });

        _logger.LogInformation("Starting {Service}", nameof(DataBufferService));

        var reader = _storage.Reader;
        var batch = new List<RawIrcMessage>(capacity: MaxBatchSize);

        try
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                var deadline = DateTime.UtcNow + MaxBufferWindow;

                // Drain whatever is available up to the deadline or the max
                // batch size, whichever comes first.
                while (batch.Count < MaxBatchSize
                    && DateTime.UtcNow < deadline
                    && reader.TryRead(out var item))
                {
                    batch.Add(item);
                }

                if (batch.Count == 0)
                {
                    // Woken by WaitToReadAsync but the message was consumed
                    // between the wait and the drain — loop back to wait.
                    continue;
                }

                try
                {
                    await HandleBufferedMessagesAsync(batch, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Flush of {Count} messages failed; continuing", batch.Count);
                }
                finally
                {
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown
        }

        _logger.LogInformation("Shutting down {Service}", nameof(DataBufferService));
    }

    private async Task HandleBufferedMessagesAsync(
        List<RawIrcMessage> messages, CancellationToken cancellationToken)
    {
        using var activity = ChatTelemetry.ActivitySource.StartActivity(
            "databuffer.flush", ActivityKind.Internal);
        activity?.SetTag("messaging.batch.message_count", messages.Count);

        var stopwatch = Stopwatch.StartNew();

        await using var context = await _repository.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var savedMessages = new List<(RawIrcMessage Raw, ChatMessage Entity, UserRef User, ChannelRef Channel)>(messages.Count);
        var newlySeenUsers = new Dictionary<string, UserRef>(StringComparer.Ordinal);
        var newlySeenChannels = new Dictionary<string, ChannelRef>(StringComparer.Ordinal);

        foreach (var m in messages)
        {
            var channel = await _repository.GetOrCreateChannelAsync(context, m.Channel, cancellationToken);
            var user = await _repository.GetOrCreateUserAsync(context, m.Sender, cancellationToken);

            var entity = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChannelName = m.Channel,
                CreatedUtc = DateTime.UtcNow,
                Message = m.Message,
                UserId = user.Id,
                ChannelId = channel.Id,
            };

            await context.ChatMessages.AddAsync(entity, cancellationToken);

            savedMessages.Add((m, entity, user, channel));
            newlySeenUsers.TryAdd(user.UserName, user);
            newlySeenChannels.TryAdd(channel.ChannelName, channel);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _repository.PromoteToCacheAsync(
            newlySeenUsers.Values, newlySeenChannels.Values, cancellationToken);

        foreach (var (raw, entity, userRef, channelRef) in savedMessages)
        {
            await _eventSender.SendAsync(raw.Channel, new ChatMessage
            {
                Id = entity.Id,
                ChannelName = entity.ChannelName,
                Message = entity.Message,
                CreatedUtc = entity.CreatedUtc,
                UserId = userRef.Id,
                User = new User
                {
                    Id = userRef.Id,
                    UserName = userRef.UserName,
                    CreatedUtc = userRef.CreatedUtc,
                },
                ChannelId = channelRef.Id,
                Channel = new Channel
                {
                    Id = channelRef.Id,
                    ChannelName = channelRef.ChannelName,
                    CreatedUtc = channelRef.CreatedUtc,
                },
            }, cancellationToken);
        }

        stopwatch.Stop();
        ChatTelemetry.BufferFlushDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
        ChatTelemetry.BufferFlushSize.Record(messages.Count);
    }
}
