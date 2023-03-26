using Data.ChatKnutDB;
using Data.ChatKnutDB.Repositories;

using Microsoft.EntityFrameworkCore;

namespace Backend.Api.Extensions;

internal static class DatabaseExtensions
{
    public static WebApplicationBuilder ConfigureDatabase(
        this WebApplicationBuilder builder)
    {
        // Setup local development database with Sqlite
        if (builder.Environment.IsDevelopment())
        {
            builder.Services
                .AddDbContextPool<ChatKnutDBContext>(options =>
                    options.UseSqlite("Data Source=ChatKnut.db"));
        }

        // Setup repositories as scoped services
        builder.RegisterRepositories();

        return builder;
    }

    private static WebApplicationBuilder RegisterRepositories(
        this WebApplicationBuilder builder)
    {
        builder.Services
            .AddScoped<ChannelRepository>();

        builder.Services
            .AddScoped<ChatMessageRepository>();

        builder.Services
            .AddScoped<UserRepository>();

        return builder;
    }
}