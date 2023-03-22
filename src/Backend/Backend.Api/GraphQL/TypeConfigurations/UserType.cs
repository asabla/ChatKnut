using Data.StoreObjects.Models;

namespace Backend.Api.GraphQL.TypeConfigurations;

public class UserType : ObjectType<User>
{
    protected override void Configure(IObjectTypeDescriptor<User> descriptor)
    {
        descriptor
            .Description("Some description about User");

        descriptor
            .Field(x => x.CreatedUtc)
            .Description("When User was first seen and registered");

        descriptor
            .Field(x => x.Id)
            .Description("Unique identifier (as GUID) for Users");

        descriptor
            .Field(x => x.Messages)
            .Description("All registered chat messages saved for given user");

        descriptor
            .Field(x => x.UserName)
            .Description("Unique user name for registered user");

        descriptor
            .Field(x => x.Messages)
            .UseOffsetPaging()
            .UseProjection()
            .UseFiltering()
            .UseSorting();
    }
}
