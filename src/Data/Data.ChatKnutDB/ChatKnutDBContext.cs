using Data.ChatKnutDB.ModelsConfigurations;
using Data.StoreObjects.Models;

using Microsoft.EntityFrameworkCore;

namespace Data.ChatKnutDB;

public class ChatKnutDBContext : DbContext
{
    public ChatKnutDBContext(DbContextOptions<ChatKnutDBContext> options)
        : base(options) { }

    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ConfigureChannel()
            .ConfigureChatMessage()
            .ConfigureUser();
    }
}