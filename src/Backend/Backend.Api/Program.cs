using ChatKnut.Backend.Api.GraphQL;
using ChatKnut.Common.TwitchChat;
using ChatKnut.Data.Chat;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPooledDbContextFactory<ChatKnutDbContext>(options
    => options.UseSqlite("Data Source=ChatKnut.db"));

// Caching things
builder.Services
    .AddMemoryCache();
builder.Services
    .AddInMemorySubscriptions();

// Background service setup
builder.Services
    .AddSingleton<ChatService>();
builder.Services
    .AddHostedService(sp => sp.GetService<ChatService>());

// GraphQL setup
builder.Services
    .AddGraphQLServer()
        .InitializeOnStartup()
        .RegisterDbContext<ChatKnutDbContext>(DbContextKind.Pooled)
        .RegisterService<ChatService>()
        .SetPagingOptions(new HotChocolate.Types.Pagination.PagingOptions
        {
            MaxPageSize = 200
        })
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddInMemoryQueryStorage()
    .UseAutomaticPersistedQueryPipeline();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseWebSockets();

app.MapGraphQL();

app.Run();