using Data.StoreObjects.Models;

namespace Backend.Api.GraphQL.TypeConfigurations;

public class ChatMessageType : ObjectType<ChatMessage>
{
    protected override void Configure(IObjectTypeDescriptor<ChatMessage> descriptor)
    {
        descriptor
            .Description("Some description about ChatMessages");

        descriptor
            .Field(x => x.Channel)
            .Description("Which channel message was registered in");

        descriptor
            .Field(x => x.ChannelId)
            .Description("Given Channel Id (GUID) which message was registered in");

        descriptor
            .Field(x => x.ChannelName)
            .Description("Which channel name message was registered in");

        descriptor
            .Field(x => x.CreatedUtc)
            .Description("When message was seen and registered");

        descriptor
            .Field(x => x.Id)
            .Description("Given message Id (GUID)");

        descriptor
            .Field(x => x.Message)
            .Description("Message content");

        descriptor
            .Field(x => x.User)
            .Description("Which user sent the message");

        descriptor
            .Field(x => x.UserId)
            .Description("Given user Id (GUID) who sent the message");
    }
}
