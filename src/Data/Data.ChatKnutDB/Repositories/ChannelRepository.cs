using Data.StoreObjects.Models;

using Microsoft.EntityFrameworkCore;

namespace Data.ChatKnutDB.Repositories;

public class ChannelRepository
{
    private readonly ChatKnutDBContext _context;

    public ChannelRepository(ChatKnutDBContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Channel>> GetChannelsAsync(
        CancellationToken cancellationToken = default)
            => await _context.Channels
                .ToListAsync(cancellationToken: cancellationToken);
}
