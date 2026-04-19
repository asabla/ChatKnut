using ChatKnut.Data.Chat;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChatKnut.Backend.Api;

// Used by the EF tooling (dotnet ef migrations add …) to construct a DbContext
// without booting the full Aspire host. The connection string below is only
// consulted at design time; runtime uses the Aspire-injected ConnectionStrings:chatknut.
public class ChatKnutDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ChatKnutDbContext>
{
    public ChatKnutDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ChatKnutDbContext>()
            .UseNpgsql("Host=localhost;Database=chatknut;Username=postgres;Password=postgres")
            .Options;

        return new ChatKnutDbContext(options);
    }
}
