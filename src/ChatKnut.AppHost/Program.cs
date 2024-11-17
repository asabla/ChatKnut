var builder = DistributedApplication.CreateBuilder(args);

// TODO: activate these when backend has migrated over from
// using SQLite to using PostgreSQL. And extracting services
// into their own services project.
// var dbServer = builder.AddPostgres("postgres")
//     .WithPgAdmin();
// var postgresDb = dbServer.AddDatabase("chatknut");
// var garnetCache = builder.AddGarnet("garnet");
// var rabbitMq = builder.AddRabbitMQ("rabbitmq")
//     .WithManagementPlugin();

builder.AddProject<Projects.Backend_Api>("backend");

builder.Build().Run();