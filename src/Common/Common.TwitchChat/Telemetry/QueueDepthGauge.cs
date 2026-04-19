using Microsoft.Extensions.Hosting;

namespace ChatKnut.Common.TwitchChat.Telemetry;

// Registers an ObservableGauge against the queue-depth of the shared
// IStorageService. Implemented as a BackgroundService so the gauge stays
// registered for the lifetime of the host and is disposed cleanly on shutdown.
public sealed class QueueDepthGauge : IHostedService, IDisposable
{
    private readonly IStorageService _storage;

    public QueueDepthGauge(IStorageService storage)
    {
        _storage = storage;

        ChatTelemetry.Meter.CreateObservableGauge(
            "chatknut.queue.depth",
            observeValue: () => _storage.Count,
            unit: "{message}",
            description: "Current number of buffered IRC messages awaiting persistence");
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public void Dispose() { }
}
