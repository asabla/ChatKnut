using ChatKnut.Backend.Api.GraphQL;
using ChatKnut.Common.TwitchChat;
using ChatKnut.Common.TwitchChat.Telemetry;
using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Services;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Setting up Aspire service defaults
builder.AddGraphQLServiceDefaults();

// Pooled DbContext factory over the Aspire-provided Postgres connection
// string. Using the factory form so HotChocolate's RegisterDbContextFactory
// gets per-request contexts instead of a shared one.
builder.Services.AddPooledDbContextFactory<ChatKnutDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("chatknut")));

// Distributed cache backed by Garnet (registered as "cache" in the AppHost).
// Redis protocol, so the StackExchange.Redis integration works against it.
builder.AddRedisDistributedCache("cache");

// Repository / storage services
builder.Services
    .AddSingleton<IChatRepository, ChatRepository>();
builder.Services
    .AddSingleton<IStorageService, StorageService>();

// Background service setup
builder.Services
    .AddSingleton<ChatService>();
builder.Services
    .AddHostedService(sp => sp.GetService<ChatService>()!);

builder.Services
    .AddSingleton<DataBufferService>();
builder.Services
    .AddHostedService(sp => sp.GetService<DataBufferService>()!);

// Register app-level telemetry instruments that need DI wiring (queue-depth gauge)
builder.Services
    .AddChatKnutTelemetry();

// GraphQL setup
builder.Services
    .AddGraphQLServer()
        .InitializeOnStartup()
        .ModifyRequestOptions(options => options.IncludeExceptionDetails
            = builder.Environment.IsDevelopment())
        .ModifyPagingOptions(options =>
        {
            options.IncludeTotalCount = true;
            options.IncludeNodesField = true;
            options.DefaultPageSize = 50;
            options.MaxPageSize = 200;
        })
        .ModifyCostOptions(options =>
        {
            options.MaxTypeCost = 1000;
            options.MaxFieldCost = 1000;
        })
    .RegisterDbContextFactory<ChatKnutDbContext>()
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddSubscriptionType<Subscription>()
    .AddInMemorySubscriptions()
    .AddCacheControl()
    .AddInstrumentation();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseWebSockets();
app.MapGraphQL();

// Setting up Aspire default endpoints
app.MapDefaultEndpoints();

await app.RunWithGraphQLCommandsAsync(args);