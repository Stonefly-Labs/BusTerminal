using BusTerminal.Api.Features.Registry.Audit;
using BusTerminal.Api.Features.Registry.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T040 + T080 / FR-037. Shared MapGroup pattern for every registry
// endpoint. Adds the auth wall (AuthN-only, no role policy per the Complexity
// Tracking deviation) + the per-route handler mappings.
public static class RegistryEndpointsBuilder
{
    public const string GroupPrefix = "/api/registry";

    public static RouteGroupBuilder MapRegistryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(GroupPrefix)
            .RequireAuthorization() // FR-037 — authentication-only; no role policy.
            .WithTags("Registry");

        // T103c — distinct environments list. Declared BEFORE the catch-all
        // routes so the `environments` literal isn't shadowed by `{id:guid}`.
        group.MapEnvironmentsEndpoint();

        // US2 search surface (T109).
        group.MapSearchEndpoint();

        // US1 CRUD surface (T075–T079, T081).
        group.MapCreateRegistryEntity();
        group.MapListRegistryEntities();
        group.MapGetRegistryEntity();
        group.MapUpdateRegistryEntity();
        group.MapDeleteRegistryEntity();
        group.MapStatusChangeEndpoint();

        // US3 audit read-only surface (T121).
        group.MapAuditEndpoint();

        return group;
    }
}
