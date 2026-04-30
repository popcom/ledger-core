using Ledger.Application.Observability;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;

namespace Ledger.Api.Observability;

/// <summary>
/// Wires Serilog as the host logger and OpenTelemetry traces and
/// metrics with the OTLP exporter so the local Aspire dashboard, a
/// Grafana stack, or any OTel collector can pick them up. The
/// <see cref="LedgerTelemetry.ActivitySource"/> and
/// <see cref="LedgerTelemetry.Meter"/> defined in the Application
/// layer are the single subscription point for the Ledger module.
/// </summary>
public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddLedgerObservability(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Host.UseSerilog((context, services, config) => config
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service.name", "ledger-core")
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

        var resource = ResourceBuilder.CreateDefault()
            .AddService(serviceName: "ledger-core",
                serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "0.0.0");

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("ledger-core"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(LedgerTelemetry.ActivitySourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(LedgerTelemetry.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter();
            });

        return builder;
    }
}
