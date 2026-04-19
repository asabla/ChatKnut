using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(
        this IHostApplicationBuilder builder,
        Action<TracerProviderBuilder>? configureTracing = null,
        bool enableLogStateParsing = false)
    {
        builder.ConfigureOpenTelemetry(configureTracing, enableLogStateParsing);

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static IHostApplicationBuilder AddGraphQLServiceDefaults(
        this IHostApplicationBuilder builder)
    {
        var isDevelopment = builder.Environment.IsDevelopment();

        // Include the GraphQL document and DataLoader keys on traces in
        // development for easier debugging; omit them in production to avoid
        // leaking query shapes / user input into telemetry backends.
        builder.Services.Configure<HotChocolate.Diagnostics.InstrumentationOptions>(options =>
        {
            options.IncludeDocument = isDevelopment;
            options.IncludeDataLoaderKeys = isDevelopment;
        });

        return builder.AddServiceDefaults(
            configureTracing: tracing => tracing.AddHotChocolateInstrumentation(),
            enableLogStateParsing: true);
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(
        this IHostApplicationBuilder builder,
        Action<TracerProviderBuilder>? configureTracing = null,
        bool enableLogStateParsing = false)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = enableLogStateParsing;
        });

        ConfigureTelemetryFilters(builder);

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("ChatKnut.*");
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddSource("ChatKnut.*");

                configureTracing?.Invoke(tracing);
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static void ConfigureTelemetryFilters(IHostApplicationBuilder builder)
    {
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
        {
            options.Filter = httpContext => !IsHealthProbePath(httpContext.Request.Path);
        });

        builder.Services.Configure<HttpClientTraceInstrumentationOptions>(options =>
        {
            options.FilterHttpRequestMessage = request =>
            {
                if (request.RequestUri is null) return true;

                // Do not trace the OTLP export loop back onto itself.
                var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint)
                    && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpUri)
                    && Uri.Compare(
                        request.RequestUri, otlpUri,
                        UriComponents.SchemeAndServer,
                        UriFormat.SafeUnescaped,
                        StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return false;
                }

                return !IsHealthProbePath(request.RequestUri.AbsolutePath);
            };
        });
    }

    private static bool IsHealthProbePath(PathString path)
        => path.StartsWithSegments("/health") || path.StartsWithSegments("/alive");

    private static bool IsHealthProbePath(string path)
        => path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/alive", StringComparison.OrdinalIgnoreCase);

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }
        else
        {
            // Aspire normally injects OTEL_EXPORTER_OTLP_ENDPOINT for its children.
            // Standalone runs without it silently drop all telemetry, which has burned
            // us before — surface it at startup so it's obvious something is off.
            builder.Logging.AddFilter("ChatKnut.ServiceDefaults", LogLevel.Warning);
            builder.Services.AddHostedService<MissingOtlpEndpointWarning>();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    private sealed class MissingOtlpEndpointWarning(ILoggerFactory loggerFactory) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var logger = loggerFactory.CreateLogger("ChatKnut.ServiceDefaults");
            logger.LogWarning(
                "OTEL_EXPORTER_OTLP_ENDPOINT is not set; OpenTelemetry data will be discarded. "
                + "This is expected when running tests in isolation, but under the Aspire AppHost "
                + "it usually means the AppHost wiring is broken.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks("/health");

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
