using ChatKnut.Data.Chat;
using ChatKnut.Migrations;

using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Runtime DbContext registered once; the migrator runs a single
// MigrateAsync and then the host exits.
builder.Services.AddDbContext<ChatKnutDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("chatknut")));

builder.Services.AddHostedService<Migrator>();

var app = builder.Build();

await app.RunAsync();