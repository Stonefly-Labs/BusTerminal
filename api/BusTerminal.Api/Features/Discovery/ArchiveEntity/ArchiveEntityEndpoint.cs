using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Discovery.Shared;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Features.Discovery.UpdateEntityMetadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace BusTerminal.Api.Features.Discovery.ArchiveEntity;

// Spec 009 / T105 + contracts/openapi.yaml#archiveEntity.
// POST /api/entities/{entityId}/archive
//
// Sets lifecycleStatus = Archived. FR-015 sticky-archive guarantees the
// status is preserved across subsequent discovery runs (enforced on the
// worker side; the endpoint here just writes the bit).
//
// Auth mirrors UpdateEntityMetadata (R-15 three-branch). If-Match required.
public static class ArchiveEntityEndpoint
{
    public static IEndpointRouteBuilder MapArchiveEntityEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapPost("/api/entities/{entityId}/archive", HandleAsync)
            .RequireAuthorization()
            .WithName("ArchiveEntity")
            .WithTags("Entities");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string entityId,
        IPublishedEntitySearchClient searchClient,
        IPublishedEntityStore entityStore,
        EntityMetadataEditorAuthorizer authorizer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return UpdateEntityMetadataEndpoint.BadRequest(context, "InvalidEntityId", "entityId is required.");
        }
        if (!context.Request.Headers.TryGetValue("If-Match", out var ifMatch) || string.IsNullOrWhiteSpace(ifMatch))
        {
            return Results.Problem(
                title: "IfMatchRequired",
                detail: "If-Match header is required.",
                statusCode: StatusCodes.Status428PreconditionRequired,
                instance: context.Request.Path,
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = "IfMatchRequired" });
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

        var authResult = await authorizer.AuthorizeAsync(
            context.User, entityId, current.Entity.ServiceAssociations, cancellationToken).ConfigureAwait(false);
        if (!authResult.Allowed)
        {
            return UpdateEntityMetadataEndpoint.Forbidden(context, entityId);
        }

        var modifiedBy = UpdateEntityMetadataEndpoint.ResolveModifiedBy(context);

        try
        {
            var updated = await entityStore.SetLifecycleStatusAsync(
                entityId, hit.Environment, LifecycleStatus.Archived, ifMatch.ToString(), modifiedBy, cancellationToken).ConfigureAwait(false);
            context.Response.Headers[HeaderNames.ETag] = updated.ETag;
            return Results.Ok(PublishedEntityResponse.From(updated.Entity));
        }
        catch (PublishedEntityConcurrencyConflictException)
        {
            return UpdateEntityMetadataEndpoint.PreconditionFailed(context, entityId);
        }
        catch (PublishedEntityNotFoundException)
        {
            return UpdateEntityMetadataEndpoint.NotFound(context, entityId);
        }
    }
}
