using BusTerminal.Api.Features.Discovery.GetDiscoveryRun;
using BusTerminal.Api.Features.Discovery.StartDiscovery;

namespace BusTerminal.Api.Features.Discovery.Shared;

// Spec 009 / T030. Route-group entry point for the discovery slice. Phase
// tasks progressively register endpoints here:
//   Phase 3 / T047 — StartDiscovery + GetDiscoveryRun           ← LANDED
//   Phase 4 / T072 — SearchEntities + GetEntityDetail
//   Phase 5 / T087 — ListDiscoveryRuns
//   Phase 6 / T110 — UpdateEntityMetadata + ArchiveEntity + 3× ServiceAssociations
//
// Per-endpoint mappers (e.g. StartDiscoveryEndpoint.MapStartDiscoveryEndpoint)
// keep their own routes; this file is the assembly point.
public static class DiscoveryEndpointsBuilder
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Phase 3 / US1.
        app.MapStartDiscoveryEndpoint();
        app.MapGetDiscoveryRunEndpoint();

        return app;
    }
}
