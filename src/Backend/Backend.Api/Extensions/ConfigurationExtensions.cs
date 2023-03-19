using Backend.Api.Settings;

using FluentValidation;

namespace Backend.Api.Extensions;

internal static class ConfigurationExtensions
{
    public static WebApplicationBuilder ConfigurationSetup(
        this WebApplicationBuilder builder)
    {
        // Configure behavior for configuration files
        builder.Configuration
            .AddJsonFile("appsettings.json",
                optional: true,
                reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json",
                optional: true,
                reloadOnChange: true);

        // Fluent validation of configuration file
        builder.Services.AddScoped<IValidator<ApiSettings>, ApiSettingsValidator>();
        builder.Services.AddOptions<ApiSettings>()
            .BindConfiguration(nameof(ApiSettings))
            .ValidateWithFluent()   // Extension method
            .ValidateOnStart();

        return builder;
    }
}