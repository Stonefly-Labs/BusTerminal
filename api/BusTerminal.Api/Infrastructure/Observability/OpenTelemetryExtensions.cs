using Azure.Monitor.OpenTelemetry.AspNetCore;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Features.Discovery.Shared.Telemetry;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
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

            // Issue #113 — UseAzureMonitor unconditionally registers three
            // resource detectors: AppService, AzureVM, and AzureContainerApps.
            // The AzureVMResourceDetector probes the Azure Instance Metadata
            // Service (http://169.254.169.254/metadata/instance) to enrich
            // telemetry with VM attributes. IMDS exists on Azure VMs/VMSS but
            // NOT on Container Apps (our hosting model), so the probe hangs on a
            // TCP connect for ~65s before failing with TaskCanceledException —
            // polluting every cold start's traces with a 65s failed dependency
            // span and delaying first telemetry export.
            //
            // The distro's detectors are internal and can't be disabled
            // individually, so we reset the resource and re-add only the
            // Container Apps detector (via the public OpenTelemetry.Resources.
            // Azure package), omitting the VM/IMDS probe. The AppService
            // detector is not re-added — it keys off WEBSITE_SITE_NAME, which
            // is never set on Container Apps, so it's a no-op for us.
            // AzureContainerAppsDetector preserves the prior cloud role
            // name (service.name = CONTAINER_APP_NAME), replica
            // (service.instance.id) and revision (service.version) — all read
            // from environment variables, no network calls. Registered AFTER
            // UseAzureMonitor so at build time Clear() drops the distro's queued
            // detectors before any of them run; AddTelemetrySdk /
            // AddEnvironmentVariableDetector then restore the OTel defaults that
            // Clear() also removed (so OTEL_RESOURCE_ATTRIBUTES still applies).
            otel.ConfigureResource(resource => resource
                .Clear()
                .AddTelemetrySdk()
                .AddEnvironmentVariableDetector()
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("telemetry.distro.name", "Azure.Monitor.OpenTelemetry.AspNetCore"),
                })
                .AddAzureContainerAppsDetector());
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
