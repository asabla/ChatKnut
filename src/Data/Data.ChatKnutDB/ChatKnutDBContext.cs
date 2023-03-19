using Microsoft.EntityFrameworkCore;
using Data.StoreObjects.Models;
using Data.ChatKnutDB.ModelsConfigurations;

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
        modelBuilder.ConfigureChannel();
        modelBuilder.ConfigureChatMessage();
        modelBuilder.ConfigureUser();
    }
}
