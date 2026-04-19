var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume();

var chatKnutDb = postgres.AddDatabase("chatknut");

var cache = builder.AddGarnet("cache")
    .WithDataVolume();

builder.AddProject<Projects.Backend_Api>("backend")
    .WithReference(chatKnutDb)
    .WithReference(cache)
    .WaitFor(chatKnutDb)
    .WaitFor(cache);

builder.Build().Run();
