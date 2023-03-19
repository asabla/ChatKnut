using Data.StoreObjects.Models;

using Microsoft.EntityFrameworkCore;

namespace Data.ChatKnutDB.ModelsConfigurations;

internal static class UserModelConfigurations
{
    public static ModelBuilder ConfigureUser(this ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<User>()
            .HasIndex(x => x.Id);

        modelBuilder
            .Entity<User>()
            .HasIndex(x => x.UserName);

        modelBuilder
            .Entity<User>()
            .HasIndex(x => x.CreatedUtc);

        return modelBuilder;
    }
}