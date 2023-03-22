namespace Backend.Api.GraphQL.TypeConfigurations;

public class QueryType : ObjectType<Query>
{
    protected override void Configure(IObjectTypeDescriptor<Query> descriptor)
    {
        descriptor
            .Field(x => x.GetUsers(default!, default))
            .Description("All registered and seen users by the system")
            .UseOffsetPaging()
            .UseProjection()
            .UseFiltering()
            .UseSorting();

        descriptor
            .Field(x => x.GetMessages(default!, default))
            .Description("All registered and seen chat messages seen by the system")
            .UseOffsetPaging()
            .UseProjection()
            .UseFiltering()
            .UseSorting();

        descriptor
            .Field(x => x.GetChannels(default!, default))
            .Description("All channels registered by the system")
            .UseOffsetPaging()
            .UseProjection()
            .UseFiltering()
            .UseSorting();
    }
}
