using ChatKnut.Data.Chat;

using Microsoft.EntityFrameworkCore;

namespace ChatKnut.Migrations;

// Runs ChatKnutDbContext migrations exactly once at startup, then requests
// host shutdown. Dependent services in the AppHost should WaitFor this
// resource so they never race ahead of the schema.
public sealed class Migrator(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<Migrator> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ChatKnutDbContext>();

            var pending = (await db.Database.GetPendingMigrationsAsync(stoppingToken)).ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("No pending migrations; schema is current");
            }
            else
            {
                logger.LogInformation(
                    "Applying {Count} pending migrations: {Migrations}",
                    pending.Count, string.Join(", ", pending));

                await db.Database.MigrateAsync(stoppingToken);

                logger.LogInformation("Migrations applied successfully");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration run failed");
            throw;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
