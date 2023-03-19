using Backend.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Load, validate and configure application
builder.ConfigurationSetup();

// GraphQL setup
builder.ConfigureGraphQLServer();

// Orleans setup
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
});

var app = builder.Build();

// Security configuration
app.UseHttpsRedirection();

// Map GraphQL endpoints
app.MapGraphQL();

// Used for possible heartbeat signals
app.MapGet("/", () => StatusCodes.Status200OK);

app.Run();