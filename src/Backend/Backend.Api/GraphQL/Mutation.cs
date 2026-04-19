using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Models;

using Microsoft.EntityFrameworkCore;

namespace ChatKnut.Backend.Api.GraphQL;

public class Mutation
{
    // TODO: restore a JoinChannel mutation once the backend can signal the
    // ingestion worker over Garnet pub-sub. For now, the ingestion worker
    // joins every channel with AutoJoin=true at startup, so setting AutoJoin
    // via ChangeAutoJoinChannel is the supported way to enrol a channel.
    public async Task<Channel> ChangeAutoJoinChannel(
        ChatKnutDbContext context,
        string channelName,
        bool autoJoin)
    {
        if (string.IsNullOrWhiteSpace(channelName))
            throw new ArgumentNullException(nameof(channelName));

        var channel = await context.Channels
            .Where(x => x.ChannelName == channelName.ToLowerInvariant())
            .FirstOrDefaultAsync()
            ?? throw new Exception($"Channel '{channelName}' was not found");

        channel.AutoJoin = autoJoin;

        await context.SaveChangesAsync();

        return channel;
    }
}
