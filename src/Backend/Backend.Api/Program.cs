using ChatKnut.Backend.Api.GraphQL;
using ChatKnut.Common.TwitchChat;
using ChatKnut.Data.Chat;

using HotChocolate.Types.Pagination;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPooledDbContextFactory<ChatKnutDbContext>(options
    => options.UseSqlite(builder
        .Configuration
        .GetConnectionString("SqliteConnectionString")!));

// Caching things
builder.Services
    .AddMemoryCache();

// Background service setup
builder.Services
    .AddSingleton<ChatService>();
builder.Services
    .AddHostedService(sp => sp.GetService<ChatService>()!);

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
        .SetPagingOptions(new PagingOptions
        {
            MaxPageSize = 200
        })
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddSubscriptionType<Subscription>()
        .AddInMemorySubscriptions()
    .AddInMemoryQueryStorage()
    .UseAutomaticPersistedQueryPipeline();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseWebSockets();

app.MapGraphQL();

app.Run();