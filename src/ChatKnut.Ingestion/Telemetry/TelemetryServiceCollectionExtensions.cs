using ChatKnut.Data.Chat.Services;

using Microsoft.Extensions.DependencyInjection;

namespace ChatKnut.Ingestion.Telemetry;

public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddChatKnutTelemetry(this IServiceCollection services)
    {
        services.AddSingleton<QueueDepthGauge>();
        services.AddHostedService(sp => sp.GetRequiredService<QueueDepthGauge>());

        return services;
    }
}