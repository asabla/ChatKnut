using ChatKnut.Data.Chat.Models;

using Microsoft.EntityFrameworkCore;

namespace ChatKnut.Data.Chat;

public class ChatKnutDbContext : DbContext
{
    public ChatKnutDbContext(DbContextOptions<ChatKnutDbContext> options)
        : base(options) { }

    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder
            .Entity<ChatMessage>()
            .HasIndex(x => x.Id)
            .IsUnique();
        builder
            .Entity<ChatMessage>()
            .HasIndex(x => x.CreatedUtc);

        builder
            .Entity<User>()
            .HasIndex(x => x.Id);
        builder
            .Entity<User>()
            .HasIndex(x => x.UserName);
        builder
            .Entity<User>()
            .HasIndex(x => x.CreatedUtc);

        builder
            .Entity<Channel>()
            .HasIndex(x => x.Id);
        builder
            .Entity<Channel>()
            .HasIndex(x => x.ChannelName);
        builder
            .Entity<Channel>()
            .HasIndex(x => x.CreatedUtc);
    }
}