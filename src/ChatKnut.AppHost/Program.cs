var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume();

var chatKnutDb = postgres.AddDatabase("chatknut");

var cache = builder.AddGarnet("cache")
    .WithDataVolume();

var migrations = builder.AddProject<Projects.ChatKnut_Migrations>("migrations")
    .WithReference(chatKnutDb)
    .WaitFor(chatKnutDb);

builder.AddProject<Projects.Backend_Api>("backend")
    .WithReference(chatKnutDb)
    .WithReference(cache)
    .WaitFor(chatKnutDb)
    .WaitFor(cache)
    .WaitForCompletion(migrations);

builder.AddProject<Projects.ChatKnut_Ingestion>("ingestion")
    .WithReference(chatKnutDb)
    .WithReference(cache)
    .WaitFor(chatKnutDb)
    .WaitFor(cache)
    .WaitForCompletion(migrations);

builder.Build().Run();