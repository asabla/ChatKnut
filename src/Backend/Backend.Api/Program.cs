using ChatKnut.Backend.Api.GraphQL;
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

// Repository shared with the ingestion worker via the same Postgres + cache.
builder.Services
    .AddSingleton<IChatRepository, ChatRepository>();

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
    .AddRedisSubscriptions()
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
