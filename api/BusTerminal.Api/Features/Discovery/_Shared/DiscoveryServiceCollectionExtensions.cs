using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Telemetry;
using BusTerminal.Api.Features.Discovery.StartDiscovery;
using BusTerminal.Api.Infrastructure.Credentials;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BusTerminal.Api.Features.Discovery.Shared;

// Spec 009 / T025. One-stop registration for the discovery slice.
// Consumed by Program.cs via `builder.Services.AddDiscoveryFeature()`.
public static class DiscoveryServiceCollectionExtensions
{
    public static IServiceCollection AddDiscoveryFeature(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Persistence ports — backed by the spec 009 Cosmos containers
        // (discovery-runs, discovery-locks) and the shared registry-entities
        // container for the PublishedEntity surface.
        services.AddSingleton<IPublishedEntityStore, CosmosPublishedEntityStore>();
        services.AddSingleton<IDiscoveryRunStore, CosmosDiscoveryRunStore>();
        services.AddSingleton<IDiscoveryLockStore, CosmosDiscoveryLockStore>();

        // R-15 authorization helper + the underlying owned-services resolver
        // (Phase 2 ships a NoOp implementation; a future spec wires the real
        // source via /api/me/owned-services).
        services.AddScoped<EntityMetadataEditorAuthorizer>();
        services.TryAddSingleton<IOwnedServicesResolver, NoOpOwnedServicesResolver>();

        // Telemetry — the Meter is registered as a singleton so its
        // instruments stay live for the host's lifetime. The ActivitySource
        // is a static field; no DI registration needed.
        services.AddSingleton<DiscoveryMeter>();

        services.TryAddSingleton(TimeProvider.System);

        // Spec 009 / Phase 3 US1 — coalescer + Service Bus publisher
        // + start-discovery namespace gate + validator. The publisher
        // depends on a configured Discovery:ServiceBus block; until that
        // block is present we fall back to a no-op publisher so the API
        // still boots (test harnesses + Phase 2-only environments).
        services.AddScoped<IDiscoveryRunCoalescer, DiscoveryRunCoalescer>();
        services.AddScoped<IStartDiscoveryNamespaceGate, StartDiscoveryNamespaceGate>();
        services.AddScoped<IValidator<StartDiscoveryRequest>, StartDiscoveryValidator>();

        if (configuration is not null)
        {
            services.Configure<DiscoveryServiceBusOptions>(
                configuration.GetSection(DiscoveryServiceBusOptions.SectionName));

            var fqns = configuration[$"{DiscoveryServiceBusOptions.SectionName}:FullyQualifiedNamespace"];
            if (!string.IsNullOrWhiteSpace(fqns))
            {
                services.AddSingleton<IDiscoveryRequestPublisher>(sp =>
                {
                    var credentialFactory = sp.GetRequiredService<IAzureCredentialFactory>();
                    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DiscoveryServiceBusOptions>>();
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ServiceBusDiscoveryRequestPublisher>>();
                    return new ServiceBusDiscoveryRequestPublisher(credentialFactory.CreateCredential(), options, logger);
                });
            }
            else
            {
                services.TryAddSingleton<IDiscoveryRequestPublisher, NoopDiscoveryRequestPublisher>();
            }
        }
        else
        {
            services.TryAddSingleton<IDiscoveryRequestPublisher, NoopDiscoveryRequestPublisher>();
        }

        return services;
    }
}

// Spec 009 / T045 fallback. When the Discovery:ServiceBus config block is
// absent (Phase 2-only deployments, integration tests) we don't want the
// API host to fail to start. The no-op publisher just logs and discards.
public sealed partial class NoopDiscoveryRequestPublisher : IDiscoveryRequestPublisher
{
    private readonly Microsoft.Extensions.Logging.ILogger<NoopDiscoveryRequestPublisher> _logger;
    public NoopDiscoveryRequestPublisher(Microsoft.Extensions.Logging.ILogger<NoopDiscoveryRequestPublisher> logger)
    {
        _logger = logger;
    }
    public Task PublishAsync(DiscoveryRequestEnvelope envelope, CancellationToken cancellationToken)
    {
        LogSwallowed(envelope.DiscoveryRunId, envelope.NamespaceId);
        return Task.CompletedTask;
    }
    [Microsoft.Extensions.Logging.LoggerMessage(EventId = 9402,
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Discovery:ServiceBus not configured; swallowing publish for run {RunId} (namespace {NamespaceId}).")]
    private partial void LogSwallowed(string runId, string namespaceId);
}
