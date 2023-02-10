using ChatKnut.Common.TwitchChat;
using ChatKnut.Data.Chat;
using ChatKnut.Data.Chat.Models;

namespace ChatKnut.Backend.Api.GraphQL;

public record JoinedChannel(string Channel)
{
    public DateTime Joined => DateTime.Now;
}

public class Mutation
{
    public async Task<JoinedChannel> JoinChannel(
        ChatService service,
        string channel)
    {
        await service.JoinChannelAsync(channel.ToLowerInvariant());

        return new JoinedChannel(channel);
    }

    public async Task<Channel> ChangeAutoJoinChannel(
        ChatKnutDbContext context,
        string channelName,
        bool autoJoin)
    {
        if (string.IsNullOrWhiteSpace(channelName))
            throw new ArgumentNullException(nameof(channelName));

        var channel = context.Channels
            .Where(x => x.ChannelName.Equals(channelName.ToLowerInvariant()))
            .FirstOrDefault();

        if (channel is null)
            throw new Exception($"Channel '{channelName}' was not found");

        channel.AutoJoin = autoJoin;

        await context.SaveChangesAsync();

        return channel;
    }

    public async Task<IEnumerable<JoinedChannel>> JoinAllChannels(
        ChatKnutDbContext context,
        ChatService service)
    {
        var result = new List<JoinedChannel>();
        var channels = context.Channels
            .Select(x => new { x.Id, x.ChannelName })
            .ToList();

        foreach (var chan in channels)
        {
            await service.JoinChannelAsync($"#{chan.ChannelName}");
            result.Add(new(chan.ChannelName));

            await Task.Delay(100);
        }

        return result;
    }
}