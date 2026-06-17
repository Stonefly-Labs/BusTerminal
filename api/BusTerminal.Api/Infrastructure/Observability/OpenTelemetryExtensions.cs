using Azure.Monitor.OpenTelemetry.AspNetCore;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Features.Discovery.Shared.Telemetry;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

namespace BusTerminal.Api.Infrastructure.Observability;

public static class OpenTelemetryExtensions
{
    public const string AppInsightsConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    public static WebApplicationBuilder AddBusTerminalTelemetry(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var environment = builder.Environment;

        var connectionString = configuration[AppInsightsConnectionStringKey]
            ?? Environment.GetEnvironmentVariable(AppInsightsConnectionStringKey);

        var serilogConfig = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service.name", "busterminal-api")
            .Enrich.WithProperty("service.environment", environment.EnvironmentName)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            serilogConfig.WriteTo.OpenTelemetry(opts =>
            {
                opts.Protocol = OtlpProtocol.HttpProtobuf;
                opts.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = "busterminal-api",
                    ["service.environment"] = environment.EnvironmentName,
                };
            });
        }

        Log.Logger = serilogConfig.CreateLogger();
        builder.Host.UseSerilog();

        var otel = builder.Services.AddOpenTelemetry();

        // Spec 004 — Cosmos SDK emits spans on its single ActivitySource. Add it
        // here so persistence operations show up in the existing trace pipeline.
        // Spec 009 / T021 — discovery ActivitySource subscribed alongside.
        otel.WithTracing(tracing => tracing
            .AddSource("Azure.Cosmos.Operation")
            .AddSource(DiscoveryActivitySource.Name));

        // Spec 004 / T153 — subscribe the validation Meter so the three finding-
        // count counters emitted by MeteredValidationEngine flow through the OTel
        // pipeline into Azure Monitor (or the local-dev exporter when no AI
        // connection string is configured). Subscription is required even when
        // UseAzureMonitor is active: UseAzureMonitor wires the exporter but does
        // not auto-subscribe to first-party Meters.
        // Spec 009 / T021 — discovery Meter subscribed alongside.
        otel.WithMetrics(metrics => metrics
            .AddMeter(ValidationMeter.Name)
            .AddMeter(DiscoveryMeter.Name));

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            otel.UseAzureMonitor(o => o.ConnectionString = connectionString);
        }
        else
        {
            builder.Services.AddLogging(logging =>
            {
                logging.AddOpenTelemetry(o =>
                {
                    o.IncludeFormattedMessage = true;
                    o.IncludeScopes = true;
                    o.ParseStateValues = true;
                });
            });
        }

        return builder;
    }
}
