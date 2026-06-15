using BusTerminal.Api.Features.Namespaces.Details;
using BusTerminal.Api.Features.Namespaces.Identity;
using BusTerminal.Api.Features.Namespaces.Inventory;
using BusTerminal.Api.Features.Namespaces.Onboarding;
using BusTerminal.Api.Features.Namespaces.Ownership;
using BusTerminal.Api.Features.Namespaces.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / research §8 + plan.md §Project Structure. Single MapGroup pattern
// for the entire `/api/namespaces/*` surface — keeps the AuthN gate uniform
// and lets the per-slice endpoint mappers focus on shape rather than
// cross-cutting concerns. Writes apply
// NamespaceAdministratorPolicy.RequireNamespaceAdministrator() per endpoint;
// reads stay AuthN-only.
//
// Per-endpoint mappers register from the leaf slices via partial-method
// patterns (e.g., OnboardingEndpoint.MapOnboardingEndpoint) and are wired in
// from this builder once the slice's per-endpoint task lands. The builder
// itself is intentionally low-ceremony so Phase 2 ships with a callable
// extension point without forcing every slice file to land at once.
public static class NamespaceEndpointsBuilder
{
    public const string GroupPrefix = "/api/namespaces";
    public const string GroupTag = "Namespaces";

    public static RouteGroupBuilder MapNamespacesGroup(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(GroupPrefix)
            .RequireAuthorization()
            .WithTags(GroupTag);

        return group;
    }

    // Spec 008 / T047. Called from Program.cs to wire every spec-008 endpoint.
    // Per-slice endpoint mappers register from leaf files (e.g.,
    // OnboardingEndpoint.MapOnboardingEndpoint) as US1/US2/US3 ship. The wiring
    // file is intentionally append-only — slices add their `.MapXyz()` call here
    // and the test layer asserts each route is reachable.
    public static IEndpointRouteBuilder MapNamespaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        _ = endpoints.MapNamespacesGroup();

        // US1 (T077–T081) — wizard-supporting endpoints. Each endpoint
        // registers its own route directly (not via the shared group)
        // because they apply per-endpoint policies (write surfaces require
        // namespace-administrator; reads stay AuthN-only).
        endpoints.MapWorkloadIdentityEndpoint();
        endpoints.MapPickerEndpoint();
        endpoints.MapPreOnboardingValidationEndpoint();
        endpoints.MapOnboardingEndpoint();

        // US2 (T102–T103) — inventory + details.
        endpoints.MapInventoryEndpoint();
        endpoints.MapDetailsEndpoint();

        // US3 (T128–T132) endpoint mappers attach below as those tasks land.
        return endpoints;
    }
}
