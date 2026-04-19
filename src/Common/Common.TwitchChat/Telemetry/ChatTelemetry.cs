using System.Diagnostics;
using System.Reflection;

namespace ChatKnut.Common.TwitchChat.Telemetry;

public static class ChatTelemetry
{
    public const string SourceName = "ChatKnut.TwitchChat";

    private static readonly string Version =
        typeof(ChatTelemetry).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

    public static readonly ActivitySource ActivitySource = new(SourceName, Version);
}
