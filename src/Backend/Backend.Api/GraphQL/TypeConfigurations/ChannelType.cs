using Data.StoreObjects.Models;

namespace Backend.Api.GraphQL.TypeConfigurations;

public class ChannelType : ObjectType<Channel>
{
    protected override void Configure(IObjectTypeDescriptor<Channel> descriptor)
    {
        descriptor
            .Description("""
                Channels which has been seen and registered by the system. Channels
                aren't registered until the first message has been seen in one.
            """);

        descriptor
            .Field(x => x.AutoJoin)
            .Description("If channel should be auto joined by default");

        descriptor
            .Field(x => x.ChannelName)
            .Description("Unique channel name for registered channel");

        descriptor
            .Field(x => x.CreatedUtc)
            .Description("When channel was first seen and registered");

        descriptor
            .Field(x => x.Id)
            .Description("Unique identifier (as GUID) for channels");

        descriptor
            .Field(x => x.Messages)
            .UseOffsetPaging()
            .UseProjection()
            .UseFiltering()
            .UseSorting();
    }
}