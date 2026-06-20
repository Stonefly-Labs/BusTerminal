using System.Text.Json;
using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Discovery.Shared;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace BusTerminal.Api.Features.Discovery.UpdateEntityMetadata;

// Spec 009 / T104 + contracts/openapi.yaml#updateEntityMetadata.
// PATCH /api/entities/{entityId}
//
// Pipeline:
//   1. Authenticated (RequireAuthorization).
//   2. If-Match header required.
//   3. Raw-body audit: reject any non-curated key (azureSourced, lifecycle*,
//      *Utc, *By, serviceAssociations, associatedServiceIds, associationRoles,
//      id, schemaVersion, entityType, environment, namespaceId, name,
//      displayName, compositeKey, parentEntityId, firstDiscoveredUtc,
//      lastSeenUtc, lastDiscoveryRunId, azureSourcedHash, etag).
//   4. Deserialize → UpdateEntityMetadataRequest → validate (FluentValidation).
//   5. Resolve environment via AI Search (matches GetEntityDetail pattern).
//   6. Read entity via store → get current ETag (must match If-Match).
//   7. Authorize via EntityMetadataEditorAuthorizer (R-15 three-branch).
//   8. Convert request → CuratedMetadataPatch via mapper.
//   9. Store.UpdateCuratedMetadataAsync.
//   10. Return updated entity + new ETag header.
public static class UpdateEntityMetadataEndpoint
{
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.Ordinal)
    {
        "description",
        "businessPurpose",
        "tags",
        "documentationLinks",
        "contactInformation",
        "operationalNotes",
    };

    public static IEndpointRouteBuilder MapUpdateEntityMetadataEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapPatch("/api/entities/{entityId}", HandleAsync)
            .RequireAuthorization()
            .WithName("UpdateEntityMetadata")
            .WithTags("Entities");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string entityId,
        IValidator<UpdateEntityMetadataRequest> validator,
        IPublishedEntitySearchClient searchClient,
        IPublishedEntityStore entityStore,
        EntityMetadataEditorAuthorizer authorizer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return BadRequest(context, "InvalidEntityId", "entityId is required.");
        }

        if (!context.Request.Headers.TryGetValue("If-Match", out var ifMatch) || string.IsNullOrWhiteSpace(ifMatch))
        {
            return Results.Problem(
                title: "IfMatchRequired",
                detail: "If-Match header carrying the prior ETag is required.",
                statusCode: StatusCodes.Status428PreconditionRequired,
                instance: context.Request.Path,
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = "IfMatchRequired" });
        }

        // Raw-body audit. Buffer the body so we can deserialize + key-scan
        // without ASP.NET model-binding swallowing unknown properties.
        string body;
        using (var reader = new StreamReader(context.Request.Body))
        {
            body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            return BadRequest(context, "EmptyBody", "Request body is required.");
        }

        JsonDocument? document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            return BadRequest(context, "InvalidJson", $"Body is not valid JSON: {ex.Message}");
        }
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return BadRequest(context, "InvalidBodyShape", "Body must be a JSON object.");
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!AllowedKeys.Contains(property.Name))
                {
                    return BadRequest(context, "DisallowedField",
                        $"Field '{property.Name}' is not modifiable through this endpoint. " +
                        "Azure-sourced and discovery-owned fields are read-only.");
                }
            }

            UpdateEntityMetadataRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<UpdateEntityMetadataRequest>(body);
            }
            catch (JsonException ex)
            {
                return BadRequest(context, "InvalidJson", $"Body did not deserialize: {ex.Message}");
            }
            if (request is null)
            {
                return BadRequest(context, "InvalidBodyShape", "Body deserialized to null.");
            }

            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(
                    validationResult.Errors.GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                    instance: context.Request.Path,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var patch = UpdateEntityMetadataMapper.ToPatch(request);
            if (!patch.HasAnyField)
            {
                return BadRequest(context, "NoFieldsToUpdate", "At least one curated field must be present.");
            }

            // Resolve env + current entity. Two-step lookup mirrors
            // GetEntityDetailEndpoint.
            var searchResults = await searchClient.SearchAsync(
                new PublishedEntitySearchRequest(Query: entityId, Skip: 0, Top: 5),
                cancellationToken).ConfigureAwait(false);
            var hit = searchResults.Hits.FirstOrDefault(h => string.Equals(h.Id, entityId, StringComparison.Ordinal));
            if (hit is null || string.IsNullOrEmpty(hit.Environment))
            {
                return NotFound(context, entityId);
            }

            var current = await entityStore.GetDetailAsync(entityId, hit.Environment, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                return NotFound(context, entityId);
            }

            var authResult = await authorizer.AuthorizeAsync(
                context.User, entityId, current.Entity.ServiceAssociations, cancellationToken).ConfigureAwait(false);
            if (!authResult.Allowed)
            {
                return Forbidden(context, entityId);
            }

            var modifiedBy = ResolveModifiedBy(context);

            try
            {
                var updated = await entityStore.UpdateCuratedMetadataAsync(
                    entityId, hit.Environment, patch, ifMatch.ToString(), modifiedBy, cancellationToken).ConfigureAwait(false);

                context.Response.Headers[HeaderNames.ETag] = updated.ETag;
                return Results.Ok(PublishedEntityResponse.From(updated.Entity));
            }
            catch (PublishedEntityConcurrencyConflictException)
            {
                return PreconditionFailed(context, entityId);
            }
            catch (PublishedEntityNotFoundException)
            {
                return NotFound(context, entityId);
            }
        }
    }

    internal static string ResolveModifiedBy(HttpContext context)
    {
        var sub = context.User.FindFirst("oid")?.Value
            ?? context.User.FindFirst("sub")?.Value
            ?? context.User.Identity?.Name
            ?? "00000000-0000-0000-0000-000000000000";
        return sub;
    }

    internal static IResult NotFound(HttpContext context, string entityId)
        => Results.Problem(
            title: "EntityNotFound",
            detail: $"Published entity {entityId} not found.",
            statusCode: StatusCodes.Status404NotFound,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = "EntityNotFound" });

    internal static IResult Forbidden(HttpContext context, string entityId)
        => Results.Problem(
            title: "Forbidden",
            detail: $"Caller is not authorized to edit entity {entityId}.",
            statusCode: StatusCodes.Status403Forbidden,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = "Forbidden" });

    internal static IResult PreconditionFailed(HttpContext context, string entityId)
        => Results.Problem(
            title: "PreconditionFailed",
            detail: $"If-Match ETag is stale for entity {entityId}. Refetch and retry.",
            statusCode: StatusCodes.Status412PreconditionFailed,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = "PreconditionFailed" });

    internal static IResult Conflict(HttpContext context, string code, string detail)
        => Results.Problem(
            title: code,
            detail: detail,
            statusCode: StatusCodes.Status409Conflict,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code });

    internal static IResult BadRequest(HttpContext context, string code, string detail)
        => Results.Problem(
            title: code,
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code });
}
