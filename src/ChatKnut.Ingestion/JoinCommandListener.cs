using ChatKnut.Common.Messaging;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatKnut.Ingestion;

// Forwards every "join this channel" command published by the backend
// onto the in-process ChatService so its IRC connection JOINs it.
public sealed class JoinCommandListener(
    IJoinChannelSubscriber _subscriber,
    ChatService _chatService,
    ILogger<JoinCommandListener> _logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _subscriber.SubscribeAsync(async (channelName, ct) =>
        {
            var normalized = channelName.StartsWith('#') ? channelName : $"#{channelName.ToLowerInvariant()}";
            _logger.LogInformation("Received join command for {Channel}", normalized);
            await _chatService.JoinChannelAsync(normalized);
        }, stoppingToken);
}
