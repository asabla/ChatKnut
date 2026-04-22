using System.Data.Common;
using System.Diagnostics;

using ChatKnut.Common.Messaging;
using ChatKnut.Data.Chat.Models;
using ChatKnut.Data.Chat.Services;
using ChatKnut.Ingestion.Models;
using ChatKnut.Ingestion.Telemetry;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

namespace ChatKnut.Ingestion;

public sealed class DataBufferService : BackgroundService
{
    private static readonly TimeSpan MaxBufferWindow = TimeSpan.FromMilliseconds(500);
    private const int MaxBatchSize = 1_000;

    private readonly IStorageService _storage;
    private readonly IChatRepository _repository;
    private readonly IChatMessageBus _messageBus;
    private readonly ILogger<DataBufferService> _logger;
    private readonly ResiliencePipeline _flushPipeline;

    public DataBufferService(
        IStorageService storage,
        IChatRepository repository,
        IChatMessageBus messageBus,
        ILogger<DataBufferService> logger)
    {
        _storage = storage;
        _repository = repository;
        _messageBus = messageBus;
        _logger = logger;

        // Retry transient DB failures up to three times with exponential
        // backoff and jitter. Non-DbException failures fall through to the
        // outer catch so bugs still surface instead of being papered over.
        _flushPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<DbException>().Handle<TimeoutException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Flush attempt {Attempt} failed, retrying in {Delay}",
                        args.AttemptNumber, args.RetryDelay);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

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
                    await _flushPipeline.ExecuteAsync(
                        async ct => await HandleBufferedMessagesAsync(batch, ct),
                        cancellationToken);
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

            context.ChatMessages.Add(entity);

            savedMessages.Add((m, entity, user, channel));
            newlySeenUsers.TryAdd(user.UserName, user);
            newlySeenChannels.TryAdd(channel.ChannelName, channel);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _repository.PromoteToCacheAsync(
            newlySeenUsers.Values, newlySeenChannels.Values, cancellationToken);

        foreach (var (_, entity, userRef, channelRef) in savedMessages)
        {
            await _messageBus.PublishAsync(new ChatMessage
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