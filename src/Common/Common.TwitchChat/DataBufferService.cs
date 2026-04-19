using System.Diagnostics;

using ChatKnut.Common.TwitchChat.Models;
using ChatKnut.Common.TwitchChat.Telemetry;
using ChatKnut.Data.Chat.Models;
using ChatKnut.Data.Chat.Services;

using HotChocolate.Subscriptions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatKnut.Common.TwitchChat;

public class DataBufferService(
    IChatRepository _repository,
    IStorageService _storageService,
    ITopicEventSender _eventSender,
    ILogger<DataBufferService> _logger
    ) : BackgroundService
{
    private const int BufferIntervalMs = 500;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Service"] = nameof(DataBufferService),
        });

        _logger.LogInformation("Starting {Service}", nameof(DataBufferService));

        var bufferLimitTime = DateTime.UtcNow.AddMilliseconds(BufferIntervalMs);
        var bufferedMessages = new List<RawIrcMessage>();

        // Needs an artificial delay before starting up for now
        await Task.Delay(BufferIntervalMs, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_storageService.TryTake(out var tmpRawMessage) && bufferedMessages.Count == 0)
            {
                await Task.Delay(BufferIntervalMs, cancellationToken);
                continue;
            }

            if (tmpRawMessage is not null)
                bufferedMessages.Add(tmpRawMessage);

            if (DateTime.UtcNow <= bufferLimitTime)
                continue;

            bufferLimitTime = DateTime.UtcNow.AddMilliseconds(BufferIntervalMs);

            _logger.LogInformation("Start handling {MessageCount} messages", bufferedMessages.Count);
            await HandleBufferedMessagesAsync(bufferedMessages, cancellationToken);
            _logger.LogInformation("{MessageCount} messages have been handled", bufferedMessages.Count);

            bufferedMessages.Clear();
        }

        _logger.LogInformation("Shutting down {Service}", nameof(DataBufferService));
    }

    private async Task HandleBufferedMessagesAsync(
        List<RawIrcMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0) return;

        using var activity = ChatTelemetry.ActivitySource.StartActivity(
            "databuffer.flush", ActivityKind.Internal);
        activity?.SetTag("messaging.batch.message_count", messages.Count);

        var stopwatch = Stopwatch.StartNew();

        // Fresh DbContext per flush: keeps the change tracker bounded and
        // guarantees the context is disposed before the next flush starts.
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

        // Only cache after a successful commit so evicted rows can't point at
        // users/channels that never made it into the database.
        await _repository.PromoteToCacheAsync(
            newlySeenUsers.Values, newlySeenChannels.Values, cancellationToken);

        // Fan-out to GraphQL subscribers happens after commit for the same
        // reason: subscribers only ever see persisted messages.
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
