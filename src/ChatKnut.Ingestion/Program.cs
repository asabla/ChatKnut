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

// Distributed cache backed by Garnet. The ingestion worker and the backend
// share this cache so user/channel lookups do not cause duplicate DB reads
// across services.
builder.AddRedisDistributedCache("cache");

// Shared repository and in-process queue between ChatService and DataBufferService.
builder.Services.AddSingleton<IChatRepository, ChatRepository>();
builder.Services.AddSingleton<IStorageService, StorageService>();

// Background services
builder.Services.AddSingleton<ChatService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ChatService>());

builder.Services.AddSingleton<DataBufferService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DataBufferService>());

// Register the app-level telemetry instruments (queue-depth gauge).
builder.Services.AddChatKnutTelemetry();

// Register an ITopicEventSender over the shared Garnet instance so messages
// published here are received by subscribers connected to the backend
// service. AddRedisSubscriptions binds to the IConnectionMultiplexer that
// AddRedisDistributedCache registered above.
builder.Services
    .AddGraphQLServer()
    .AddRedisSubscriptions();

var host = builder.Build();
await host.RunAsync();
