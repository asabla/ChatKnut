using Backend.Api.GraphQL;

namespace Backend.Api.Extensions;

internal static class GraphQLExtensions
{
    public static WebApplicationBuilder ConfigureGraphQLServer(
        this WebApplicationBuilder builder)
    {
        builder.Services
            .AddGraphQLServer()
                .InitializeOnStartup()
                .ModifyRequestOptions(opt =>
                {
                    opt.Complexity.Enable = true;
                    opt.Complexity.MaximumAllowed = 1500;
                })
                .SetPagingOptions(new HotChocolate.Types.Pagination.PagingOptions
                {
                    MaxPageSize = 100,
                    IncludeTotalCount = true,
                })
            .AddQueryType<Query>();

        return builder;
    }
}