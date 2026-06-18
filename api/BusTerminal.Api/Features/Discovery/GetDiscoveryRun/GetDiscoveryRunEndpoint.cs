using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Discovery.GetDiscoveryRun;

// Spec 009 / T046 + contracts/openapi.yaml#getDiscoveryRun.
// GET /api/discovery-runs/{discoveryRunId}?namespaceId={ns}
//
// namespaceId is a required query parameter — the discovery-runs container's
// PK is `/namespaceId`, so a cross-partition read would require fan-out. The
// UI always knows the namespace context (history listing or status panel), so
// this is a non-issue at the surface.
public static class GetDiscoveryRunEndpoint
{
    public static IEndpointRouteBuilder MapGetDiscoveryRunEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/api/discovery-runs/{discoveryRunId}", HandleAsync)
            .RequireAuthorization()
            .WithName("GetDiscoveryRun")
            .WithTags("Discovery");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string discoveryRunId,
        string? namespaceId,
        IDiscoveryRunStore runStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(namespaceId))
        {
            return Results.Problem(
                title: "MissingNamespaceId",
                detail: "Query parameter `namespaceId` is required.",
                statusCode: StatusCodes.Status400BadRequest,
                instance: context.Request.Path);
        }

        var run = await runStore.GetAsync(discoveryRunId, namespaceId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return Results.Problem(
                title: "DiscoveryRunNotFound",
                detail: $"Discovery run {discoveryRunId} not found in namespace {namespaceId}.",
                statusCode: StatusCodes.Status404NotFound,
                instance: context.Request.Path);
        }
        return Results.Ok(run);
    }
}
