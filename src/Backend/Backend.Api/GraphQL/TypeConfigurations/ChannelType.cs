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
            .Field(x => x.Messages)
            .UseOffsetPaging()
            .UseProjection()
            .UseFiltering()
            .UseSorting();
    }
}