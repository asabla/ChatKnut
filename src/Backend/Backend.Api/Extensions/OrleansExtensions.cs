namespace Backend.Api.Extensions;

internal static class OrleansExtensions
{
    public static WebApplicationBuilder ConfigureOrleans(
        this WebApplicationBuilder builder)
    {
        builder.Host.UseOrleans(siloBuilder
            => siloBuilder.UseLocalhostClustering());

        return builder;
    }
}