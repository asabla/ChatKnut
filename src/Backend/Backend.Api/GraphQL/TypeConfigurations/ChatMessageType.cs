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
            .Field(x => x.CreatedUtc)
            .Description("When message was seen and registered");
    }
}
