using BusTerminal.Api.Features.Discovery.GetDiscoveryRun;
using BusTerminal.Api.Features.Discovery.GetEntityDetail;
using BusTerminal.Api.Features.Discovery.ListDiscoveryRuns;
using BusTerminal.Api.Features.Discovery.SearchEntities;
using BusTerminal.Api.Features.Discovery.StartDiscovery;

namespace BusTerminal.Api.Features.Discovery.Shared;

// Spec 009 / T030. Route-group entry point for the discovery slice. Phase
// tasks progressively register endpoints here:
//   Phase 3 / T047 — StartDiscovery + GetDiscoveryRun           ← LANDED
//   Phase 4 / T072 — SearchEntities + GetEntityDetail            ← LANDED
//   Phase 5 / T087 — ListDiscoveryRuns                           ← LANDED
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

        // Phase 4 / US2.
        app.MapSearchEntitiesEndpoint();
        app.MapGetEntityDetailEndpoint();

        // Phase 5 / US3.
        app.MapListDiscoveryRunsEndpoint();

        return app;
    }
}
