using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Discovery.ListDiscoveryRuns;

// Spec 009 / T086 / US3 + contracts/openapi.yaml#listDiscoveryRuns.
// GET /api/namespaces/{namespaceId}/discovery-runs?pageSize=&continuationToken=
//
// Cosmos PK is `/namespaceId`; the composite index (/namespaceId, /startedUtc
// DESC) powers a single-partition reverse-chronological scan that the store
// hands back as `DiscoveryRunPage`. Continuation tokens are Cosmos-native
// (opaque to the API + UI). pageSize is clamped to [1, 100] (default 25)
// matching the OpenAPI contract.
public static class ListDiscoveryRunsEndpoint
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapListDiscoveryRunsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/api/namespaces/{namespaceId:guid}/discovery-runs", HandleAsync)
            .RequireAuthorization()
            .WithName("ListDiscoveryRuns")
            .WithTags("Discovery");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid namespaceId,
        int? pageSize,
        string? continuationToken,
        IDiscoveryRunStore runStore,
        CancellationToken cancellationToken)
    {
        var clamped = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);

        var page = await runStore.ListByNamespaceAsync(
            namespaceId.ToString("D"),
            clamped,
            continuationToken,
            cancellationToken).ConfigureAwait(false);

        var response = new ListDiscoveryRunsResponse(page.Items, page.ContinuationToken);
        return Results.Ok(response);
    }
}
