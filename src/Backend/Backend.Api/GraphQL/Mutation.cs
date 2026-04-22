using ChatKnut.Common.Messaging;
using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Models;

using Microsoft.EntityFrameworkCore;

namespace ChatKnut.Backend.Api.GraphQL;

public record JoinedChannel(string Channel)
{
    public DateTime Joined { get; } = DateTime.UtcNow;
}

public class Mutation
{
    // Publishes a join command on the Redis bus. The ingestion worker picks
    // it up and performs the actual IRC JOIN; this mutation does not wait
    // for the JOIN to complete on the wire.
    public async Task<JoinedChannel> JoinChannel(
        IJoinChannelBus bus,
        string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("Channel name is required", nameof(channel));

        var normalized = channel.TrimStart('#').ToLowerInvariant();

        await bus.PublishJoinAsync(normalized);
        return new JoinedChannel(normalized);
    }

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

    public async Task<IEnumerable<JoinedChannel>> JoinAllAutoJoinChannels(
        ChatKnutDbContext context,
        IJoinChannelBus bus)
    {
        var autoJoin = await context.Channels
            .Where(c => c.AutoJoin)
            .Select(c => c.ChannelName)
            .ToListAsync();

        var result = new List<JoinedChannel>(autoJoin.Count);
        foreach (var name in autoJoin)
        {
            await bus.PublishJoinAsync(name);
            result.Add(new JoinedChannel(name));
        }

        return result;
    }
}