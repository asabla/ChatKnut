using Data.StoreObjects.Models;

using Microsoft.EntityFrameworkCore;

namespace Data.ChatKnutDB.ModelsConfigurations;

internal static class ChannelModelConfigurations
{
    public static ModelBuilder ConfigureChannel(this ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<Channel>()
            .HasIndex(x => x.Id);

        modelBuilder
            .Entity<Channel>()
            .HasIndex(x => x.ChannelName);

        modelBuilder
            .Entity<Channel>()
            .HasIndex(x => x.CreatedUtc);

        return modelBuilder;
    }
}