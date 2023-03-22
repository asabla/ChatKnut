using Data.ChatKnutDB.Repositories;

using HotChocolate.Execution.Configuration;
using HotChocolate.Types.Pagination;

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
                .SetPagingOptions(new PagingOptions
                {
                    MaxPageSize = 100,
                    IncludeTotalCount = true,
                })
            .AddTypes()                 // Generated with ModulesInfo.cs
            .AddFiltering()
            .AddProjections()
            .AddSorting()
            .RegisterRepositories();    // Extension method

        return builder;
    }

    private static IRequestExecutorBuilder RegisterRepositories(
        this IRequestExecutorBuilder builder)
    {
        builder
            .RegisterService<ChannelRepository>(ServiceKind.Resolver)
            .RegisterService<ChatMessageRepository>(ServiceKind.Resolver)
            .RegisterService<UserRepository>(ServiceKind.Resolver);

        return builder;
    }
}