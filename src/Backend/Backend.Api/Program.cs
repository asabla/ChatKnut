using Backend.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Load configuration and setup application
builder
    .ConfigurationSetup()
    .ConfigureDatabase()
    .ConfigureGraphQLServer()
    .ConfigureOrleans();

var app = builder.Build();

// Security configuration
app.UseHttpsRedirection();

// Map GraphQL endpoints
app.MapGraphQL();

// Used for possible heartbeat signals
app.MapGet("/", () => StatusCodes.Status200OK);

app.Run();