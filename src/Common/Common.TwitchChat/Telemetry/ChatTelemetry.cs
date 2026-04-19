using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace ChatKnut.Common.TwitchChat.Telemetry;

public static class ChatTelemetry
{
    public const string SourceName = "ChatKnut.TwitchChat";

    private static readonly string Version =
        typeof(ChatTelemetry).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

    public static readonly ActivitySource ActivitySource = new(SourceName, Version);

    public static readonly Meter Meter = new(SourceName, Version);

    public static readonly Counter<long> MessagesReceived =
        Meter.CreateCounter<long>(
            "chatknut.messages.received",
            unit: "{message}",
            description: "Number of IRC messages accepted for storage");

    public static readonly Counter<long> MessagesDropped =
        Meter.CreateCounter<long>(
            "chatknut.messages.dropped",
            unit: "{message}",
            description: "Number of IRC messages rejected before storage");

    public static readonly Histogram<double> BufferFlushDuration =
        Meter.CreateHistogram<double>(
            "chatknut.buffer.flush.duration",
            unit: "ms",
            description: "Duration of a DataBufferService flush cycle");

    public static readonly Histogram<long> BufferFlushSize =
        Meter.CreateHistogram<long>(
            "chatknut.buffer.flush.size",
            unit: "{message}",
            description: "Number of messages persisted in a single flush cycle");
}
