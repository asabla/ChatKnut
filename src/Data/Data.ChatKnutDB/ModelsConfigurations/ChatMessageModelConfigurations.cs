using Data.StoreObjects.Models;

using Microsoft.EntityFrameworkCore;

namespace Data.ChatKnutDB.ModelsConfigurations;

internal static class ChatMessageModelConfigurations
{
    public static ModelBuilder ConfigureChatMessage(this ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<ChatMessage>()
            .HasIndex(x => x.Id)
            .IsUnique();

        modelBuilder
            .Entity<ChatMessage>()
            .HasIndex(x => x.CreatedUtc);

        return modelBuilder;
    }
}
