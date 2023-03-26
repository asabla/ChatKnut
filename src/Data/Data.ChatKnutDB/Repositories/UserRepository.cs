using Data.StoreObjects.Models;

using Microsoft.EntityFrameworkCore;

namespace Data.ChatKnutDB.Repositories;

public class UserRepository
{
    private readonly ChatKnutDBContext _context;

    public UserRepository(ChatKnutDBContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<User>> GetUsersAsync(
        CancellationToken cancellationToken = default)
            => await _context.Users
                .ToListAsync(cancellationToken: cancellationToken);
}