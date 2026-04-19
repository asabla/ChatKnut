using Microsoft.Extensions.DependencyInjection;

namespace ChatKnut.Common.Messaging;

public static class MessageBusServiceCollectionExtensions
{
    // Registers publishers and subscribers for both cross-service topics. The
    // caller must have already registered an IConnectionMultiplexer (Aspire's
    // AddRedisDistributedCache or AddRedisClient does this).
    public static IServiceCollection AddChatKnutMessageBus(this IServiceCollection services)
    {
        services.AddSingleton<RedisChatMessageBus>();
        services.AddSingleton<IChatMessageBus>(sp => sp.GetRequiredService<RedisChatMessageBus>());
        services.AddSingleton<IChatMessageSubscriber>(sp => sp.GetRequiredService<RedisChatMessageBus>());

        services.AddSingleton<RedisJoinChannelBus>();
        services.AddSingleton<IJoinChannelBus>(sp => sp.GetRequiredService<RedisJoinChannelBus>());
        services.AddSingleton<IJoinChannelSubscriber>(sp => sp.GetRequiredService<RedisJoinChannelBus>());

        return services;
    }
}
