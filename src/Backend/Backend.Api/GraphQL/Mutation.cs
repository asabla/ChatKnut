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
        IJoinChannelBus bus,
        string channelName,
        bool autoJoin)
    {
        if (string.IsNullOrWhiteSpace(channelName))
            throw new ArgumentException("Channel name is required", nameof(channelName));

        var normalized = channelName.TrimStart('#').ToLowerInvariant();

        // Upsert: create the row the first time a channel is marked autojoin
        // rather than forcing callers to wait for ingestion to have seen a
        // message there. Guid is client-generated; no insert round-trip races.
        var channel = await context.Channels
            .Where(x => x.ChannelName == normalized)
            .FirstOrDefaultAsync();

        if (channel is null)
        {
            channel = new Channel
            {
                Id = Guid.NewGuid(),
                ChannelName = normalized,
                CreatedUtc = DateTime.UtcNow,
                AutoJoin = autoJoin,
            };
            context.Channels.Add(channel);
        }
        else
        {
            channel.AutoJoin = autoJoin;
        }

        await context.SaveChangesAsync();

        // Notify ingestion so it joins right away instead of waiting for the
        // next reconnect to re-scan the AutoJoin set.
        if (autoJoin)
            await bus.PublishJoinAsync(normalized);

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