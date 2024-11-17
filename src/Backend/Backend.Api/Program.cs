using ChatKnut.Backend.Api.GraphQL;
using ChatKnut.Common.TwitchChat;
using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Services;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Setting up Aspire service defaults
builder.AddGraphQLServiceDefaults();

// Is required now for ChatService, might need to be refactored to support
// an ordinary pooled db context instead
builder.Services.AddPooledDbContextFactory<ChatKnutDbContext>(options
    => options.UseSqlite(builder.Configuration
        .GetConnectionString("SqliteConnectionString")));

// Caching things
builder.Services
    .AddMemoryCache();

// Singleton services
builder.Services
    .AddSingleton<IQueueService, QueueService>();
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