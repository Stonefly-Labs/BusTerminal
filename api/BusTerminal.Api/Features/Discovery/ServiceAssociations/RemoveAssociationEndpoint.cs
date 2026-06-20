using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Features.Discovery.UpdateEntityMetadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace BusTerminal.Api.Features.Discovery.ServiceAssociations;

// Spec 009 / T108 + contracts/openapi.yaml#removeEntityAssociation.
// DELETE /api/entities/{entityId}/associations/{associationId}
//
// Symmetric to AddAssociation. R-15 three-branch auth, If-Match required,
// returns 204 on success.
public static class RemoveAssociationEndpoint
{
    public static IEndpointRouteBuilder MapRemoveAssociationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapDelete("/api/entities/{entityId}/associations/{associationId}", HandleAsync)
            .RequireAuthorization()
            .WithName("RemoveEntityAssociation")
            .WithTags("Associations");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string entityId,
        string associationId,
        IPublishedEntitySearchClient searchClient,
        IPublishedEntityStore entityStore,
        EntityMetadataEditorAuthorizer authorizer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return UpdateEntityMetadataEndpoint.BadRequest(context, "InvalidEntityId", "entityId is required.");
        }
        if (string.IsNullOrWhiteSpace(associationId))
        {
            return UpdateEntityMetadataEndpoint.BadRequest(context, "InvalidAssociationId", "associationId is required.");
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
            var updated = await entityStore.RemoveAssociationAsync(
                entityId, hit.Environment, associationId, ifMatch.ToString(), modifiedBy, cancellationToken).ConfigureAwait(false);
            context.Response.Headers[HeaderNames.ETag] = updated.ETag;
            return Results.NoContent();
        }
        catch (ServiceAssociationNotFoundException)
        {
            return Results.Problem(
                title: "AssociationNotFound",
                detail: $"Association {associationId} not found on entity {entityId}.",
                statusCode: StatusCodes.Status404NotFound,
                instance: context.Request.Path,
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = "AssociationNotFound" });
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
