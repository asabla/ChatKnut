using Data.StoreObjects.Models;

using Microsoft.EntityFrameworkCore;

namespace Data.ChatKnutDB.Repositories;

public class ChatMessageRepository
{
    private readonly ChatKnutDBContext _context;

    public ChatMessageRepository(ChatKnutDBContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetChatMessagesAsync(
        CancellationToken cancellationToken = default)
            => await _context.ChatMessages
                .ToListAsync(cancellationToken: cancellationToken);
}
