using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Features.Discovery.UpdateEntityMetadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Discovery.ServiceAssociations;

// Spec 009 / T106 + contracts/openapi.yaml#listEntityAssociations.
// GET /api/entities/{entityId}/associations
//
// Lightweight read — re-uses the GetEntityDetail two-step resolve so the
// caller doesn't need to thread `environment`. Authenticated-only; the
// associations are not sensitive in v1 (no PII).
public static class ListAssociationsEndpoint
{
    public static IEndpointRouteBuilder MapListAssociationsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/api/entities/{entityId}/associations", HandleAsync)
            .RequireAuthorization()
            .WithName("ListEntityAssociations")
            .WithTags("Associations");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string entityId,
        IPublishedEntitySearchClient searchClient,
        IPublishedEntityStore entityStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return UpdateEntityMetadataEndpoint.BadRequest(context, "InvalidEntityId", "entityId is required.");
        }

        var searchResults = await searchClient.SearchAsync(
            new PublishedEntitySearchRequest(Query: entityId, Skip: 0, Top: 5),
            cancellationToken).ConfigureAwait(false);
        var hit = searchResults.Hits.FirstOrDefault(h => string.Equals(h.Id, entityId, StringComparison.Ordinal));
        if (hit is null || string.IsNullOrEmpty(hit.Environment))
        {
            return UpdateEntityMetadataEndpoint.NotFound(context, entityId);
        }

        var current = await entityStore.GetDetailAsync(entityId, hit.Environment, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return UpdateEntityMetadataEndpoint.NotFound(context, entityId);
        }

        return Results.Ok(current.Entity.ServiceAssociations);
    }
}
