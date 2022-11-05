using ChatKnut.Common.TwitchChat;

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
}