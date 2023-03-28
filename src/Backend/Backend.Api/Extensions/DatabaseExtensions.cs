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
        builder
            .RegisterRepository<ChannelRepository>()
            .RegisterRepository<ChatMessageRepository>()
            .RegisterRepository<UserRepository>();

        return builder;
    }

    private static WebApplicationBuilder RegisterRepository<TRepo>(
        this WebApplicationBuilder builder)
        where TRepo : class
    {
        builder.Services
            .AddScoped<TRepo>();

        return builder;
    }
}