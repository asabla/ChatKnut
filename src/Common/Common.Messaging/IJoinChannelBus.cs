namespace ChatKnut.Common.Messaging;

// Backend → ingestion command channel. The backend publishes a Twitch
// channel name ("foo" or "#foo") it wants the ingestion worker to JOIN.
public interface IJoinChannelBus
{
    Task PublishJoinAsync(string channelName, CancellationToken cancellationToken = default);
}

public interface IJoinChannelSubscriber
{
    Task SubscribeAsync(
        Func<string, CancellationToken, Task> handler,
        CancellationToken cancellationToken);
}