using ChatKnut.Backend.Api.GraphQL;
using ChatKnut.Common.TwitchChat;
using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Services;

using HotChocolate.Types.Pagination;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Is required now for ChatService, might need to be refactored to support
// an ordinary pooled db context instead
builder.Services.AddPooledDbContextFactory<ChatKnutDbContext>(options
    => options.UseSqlite(builder
        .Configuration
        .GetConnectionString("SqliteConnectionString")));

// Caching things
builder.Services
    .AddMemoryCache();

// Singleton services
builder.Services
    .AddSingleton<IDataService, DataService>();
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
        .ModifyRequestOptions(opt =>
        {
            opt.Complexity.Enable = true;
            opt.Complexity.MaximumAllowed = 1500;
        })
        .RegisterDbContext<ChatKnutDbContext>(DbContextKind.Pooled)
        .RegisterService<ChatService>()
        .RegisterService<DataBufferService>()
        .SetPagingOptions(new PagingOptions
        {
            MaxPageSize = 200,
            IncludeTotalCount = true
        })
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddSubscriptionType<Subscription>()
    .AddInMemorySubscriptions()
    .AddCacheControl()
    .AddInMemoryQueryStorage()
    .UseAutomaticPersistedQueryPipeline();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseWebSockets();

app.MapGraphQL();

await app.RunWithGraphQLCommandsAsync(args);