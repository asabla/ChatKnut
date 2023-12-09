using ChatKnut.Common.TwitchChat.Models;
using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Models;
using ChatKnut.Data.Chat.Services;

using HotChocolate.Subscriptions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatKnut.Common.TwitchChat;

public class DataBufferService(
    IDataService _dataService,
    IStorageService _storageService,
    ITopicEventSender _eventSender,
    ILogger<DataBufferService> _logger
    ) : BackgroundService
{
    private ChatKnutDbContext _dbContext = null!;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Make sure database has been created and have latest migrations");
        _dbContext = await _dataService.CreateDbContext();

        if (await _dbContext.Database.EnsureCreatedAsync() is true)
        {
            _logger.LogWarning("Applying migrations after creating database");
            await _dbContext.Database.MigrateAsync();
        }

        _logger.BeginScope($"[{nameof(DataBufferService)}]");
        _logger.LogInformation($"Starting {nameof(DataBufferService)}...");

        int bufferInterval = 500;
        DateTime bufferLimitTime = DateTime.Now.AddMilliseconds(bufferInterval);
        List<RawIrcMessage> bufferedMessages = new();

        // Needs an artificial delay before starting up for now
        await Task.Delay(bufferInterval);

        // Makes sure message buffer is empty before shutting down background service
        while (cancellationToken.IsCancellationRequested is false)
        {
            if (_storageService.TryTake(out RawIrcMessage? tmpRawMessage) is false
                && bufferedMessages.Count == 0)
            {
                // This may be the starting state, no need to loop through it
                // as fast as possible
                await Task.Delay(bufferInterval);

                continue;
            }
            else if (tmpRawMessage is not null)
            {
                bufferedMessages.Add(tmpRawMessage);
            }

            // If 500ms has taken since last run, then 
            // try insert message into db
            if (DateTime.Now <= bufferLimitTime)
                continue;

            bufferLimitTime = DateTime.Now.AddMilliseconds(bufferInterval);

            _logger.LogInformation($"Start handling {bufferedMessages.Count} number of messages");
            await HandleBufferedMessages(bufferedMessages);

            _logger.LogInformation($"Messages ({bufferedMessages.Count}) has been handled");
            bufferedMessages.Clear();
        }

        _logger.LogInformation($"Shutting down {nameof(DataBufferService)}...");
    }

    private async Task HandleBufferedMessages(List<RawIrcMessage> messages)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        foreach (var m in messages)
        {
            var channel = await _dataService.GetOrCreateChannelAsync(m.Channel);
            var user = await _dataService.GetOrCreateUserAsync(m.Sender);

            var dbMessage = _dataService.InsertMessage(new()
            {
                Id = Guid.NewGuid(),
                ChannelName = m.Channel,
                CreatedUtc = DateTime.UtcNow,
                Message = m.Message,
                UserId = user.Id,
                ChannelId = channel.Id
            });

            if (dbMessage is not null)
            {
                await _eventSender.SendAsync(m.Channel, new ChatMessage
                {
                    Id = dbMessage.Entity.Id,
                    ChannelName = dbMessage.Entity.ChannelName,
                    Message = dbMessage.Entity.Message,
                    CreatedUtc = dbMessage.Entity.CreatedUtc,
                    UserId = user.Id,
                    User = user,
                    ChannelId = channel.Id,
                    Channel = channel 
                });
            }
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        // TODO: add support for live reporting of message to 
        // graphql subscription
        // -------------------------------------------------------
        // Manual mapping is needed for avoiding null values for
        // user and channel entities
        // -------------------------------------------------------
    }
}