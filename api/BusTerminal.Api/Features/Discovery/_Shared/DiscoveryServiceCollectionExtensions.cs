using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Telemetry;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BusTerminal.Api.Features.Discovery.Shared;

// Spec 009 / T025. One-stop registration for the discovery slice.
// Consumed by Program.cs via `builder.Services.AddDiscoveryFeature()`.
public static class DiscoveryServiceCollectionExtensions
{
    public static IServiceCollection AddDiscoveryFeature(this IServiceCollection services)
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

        return services;
    }
}
