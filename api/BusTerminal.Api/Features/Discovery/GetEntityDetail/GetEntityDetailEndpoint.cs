using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace BusTerminal.Api.Features.Discovery.GetEntityDetail;

// Spec 009 / T071 + contracts/openapi.yaml#getEntityDetail.
// GET /api/entities/{entityId}
//
// Two-step lookup (data-model.md §5):
//   1. Search AI Search for the id to resolve the document's `environment`
//      (the Cosmos partition key). The id pattern `pe_<base32>{24}` is
//      globally unique within an environment partition; the search index
//      is the cheapest way to find the right one.
//   2. Single-partition Cosmos read by (id, environment) → full document
//      + Cosmos ETag echoed back via the HTTP `ETag` header.
//
// Returns 404 when either step misses (the search hit is absent OR the
// Cosmos doc isn't a published entity yet — see CosmosPublishedEntityStore
// notes on legacy spec 006 documents).
public static class GetEntityDetailEndpoint
{
    public static IEndpointRouteBuilder MapGetEntityDetailEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/api/entities/{entityId}", HandleAsync)
            .RequireAuthorization()
            .WithName("GetEntityDetail")
            .WithTags("Entities");

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
            return Results.Problem(
                title: "InvalidEntityId",
                detail: "entityId is required.",
                statusCode: StatusCodes.Status400BadRequest,
                instance: context.Request.Path);
        }

        // Step 1: resolve the environment via AI Search. We do a narrow
        // search by id (single-result expected) and trust the index.
        var searchRequest = new PublishedEntitySearchRequest(
            Query: entityId,
            Skip: 0,
            Top: 5);
        var searchResults = await searchClient.SearchAsync(searchRequest, cancellationToken).ConfigureAwait(false);
        var hit = searchResults.Hits.FirstOrDefault(h => string.Equals(h.Id, entityId, StringComparison.Ordinal));
        if (hit is null || string.IsNullOrEmpty(hit.Environment))
        {
            return NotFound(context, entityId);
        }

        // Step 2: single-partition Cosmos read.
        var detail = await entityStore.GetDetailAsync(entityId, hit.Environment, cancellationToken).ConfigureAwait(false);
        if (detail is null)
        {
            return NotFound(context, entityId);
        }

        if (!string.IsNullOrEmpty(detail.ETag))
        {
            context.Response.Headers[HeaderNames.ETag] = detail.ETag;
        }
        context.Response.Headers[HeaderNames.LastModified] =
            detail.Entity.LastModifiedUtc.ToString("R");

        return Results.Ok(detail.Entity);
    }

    private static IResult NotFound(HttpContext context, string entityId)
        => Results.Problem(
            title: "EntityNotFound",
            detail: $"Published entity {entityId} not found.",
            statusCode: StatusCodes.Status404NotFound,
            instance: context.Request.Path);
}
