using ChatKnut.Common.Messaging;
using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Services;
using ChatKnut.Ingestion;
using ChatKnut.Ingestion.Telemetry;

using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Pooled factory over the Aspire-provided connection string; ingestion
// opens one DbContext per flush so the change tracker stays bounded.
builder.Services.AddPooledDbContextFactory<ChatKnutDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("chatknut")));

// Distributed cache + underlying IConnectionMultiplexer backed by Garnet.
// The message bus reuses the same multiplexer for pub-sub channels.
builder.AddRedisDistributedCache("cache");

// Cross-service bus for chat fan-out and join commands.
builder.Services.AddChatKnutMessageBus();

// Shared repository and in-process queue between ChatService and DataBufferService.
builder.Services.AddSingleton<IChatRepository, ChatRepository>();
builder.Services.AddSingleton<IStorageService, StorageService>();

// Background services
builder.Services.AddSingleton<ChatService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ChatService>());

builder.Services.AddSingleton<DataBufferService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DataBufferService>());

// Listens on Redis for "join this channel" commands from the backend and
// forwards them to the running ChatService.
builder.Services.AddHostedService<JoinCommandListener>();

// Register the app-level telemetry instruments (queue-depth gauge).
builder.Services.AddChatKnutTelemetry();

var host = builder.Build();
await host.RunAsync();