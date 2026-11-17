using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Demo.ServiceDefaults;

public static class TelemetryExtensions
{
    public static WebApplicationBuilder AddDemoTelemetry(
        this WebApplicationBuilder builder,
        string serviceName)
    {
        var serviceVersion = builder.Configuration["SERVICE_VERSION"] ?? "0.1.0";

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;
            options.AddOtlpExporter();
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("Demo.Worker")
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("Demo.Order", "Demo.Worker")
                .AddOtlpExporter((_, readerOptions) =>
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10_000));

        return builder;
    }
}