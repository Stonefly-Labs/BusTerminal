using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T040 / FR-037. Shared MapGroup pattern for every registry
// endpoint. Adds the auth wall + the trace-context response surface; the per-
// route mapping (T080) layers in the actual handlers. Authorization policy is
// AuthN-only (no role policy) per the Complexity Tracking deviation in plan.md.
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

        // Per-route mapping is added in T080 (Phase 3 US1) once the endpoint
        // handlers exist. This shared method is the single attachment point.
        return group;
    }
}
