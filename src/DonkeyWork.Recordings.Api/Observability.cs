using DonkeyWork.Recordings.Audio.Core.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DonkeyWork.Recordings.Api;

public static class Observability
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        // OTLP export is opt-in: a no-op until OTEL_EXPORTER_OTLP_ENDPOINT is set (then it
        // reads the standard OTEL_ env vars for endpoint/protocol/headers). Instrumentation is
        // always wired so spans/metrics/logs exist locally even without a collector.
        var otlpConfigured = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "donkeywork-recordings-api",
                serviceVersion: typeof(Observability).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(AudioDiagnostics.ActivitySourceName)
                .AddSource("Npgsql"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(AudioDiagnostics.MeterName))
            .WithLogging(configureBuilder: null, configureOptions: options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
            });

        if (otlpConfigured)
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }
}
