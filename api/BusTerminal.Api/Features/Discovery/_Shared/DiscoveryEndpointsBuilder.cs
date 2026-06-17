namespace BusTerminal.Api.Features.Discovery.Shared;

// Spec 009 / T030. Empty route-group shell for the discovery slice.
// Subsequent phases populate this method:
//   Phase 3 / T047 — StartDiscovery + GetDiscoveryRun
//   Phase 4 / T072 — SearchEntities + GetEntityDetail
//   Phase 5 / T087 — ListDiscoveryRuns
//   Phase 6 / T110 — UpdateEntityMetadata + ArchiveEntity + 3× ServiceAssociations
//
// Until those tasks land, MapDiscoveryEndpoints is a no-op. Program.cs
// already calls it so the wiring stays stable as routes get added.
public static class DiscoveryEndpointsBuilder
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        // Routes are registered by the per-user-story tasks listed above.
        return app;
    }
}
