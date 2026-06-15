using System.Text.Json;
using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace BusTerminal.Api.Features.Namespaces.Metadata;

// Spec 008 / T128 + FR-005 + contracts/namespace-onboarding-api.yaml#/UpdateMetadataRequest.
// PUT /api/namespaces/{id}/metadata.
//
// Pipeline:
//   1. namespace-administrator role gate (via NamespaceAdministratorPolicy).
//   2. If-Match required — 428 PreconditionRequired without it.
//   3. Load the persisted namespace; 404 if missing OR not source=Onboarded.
//   4. Capture the raw JSON body so UpdateMetadataValidator can reject
//      Azure-identifier fields (FR-005).
//   5. FluentValidation of UpdateMetadataRequest.
//   6. `with`-update the persisted document, preserving every Azure-identifier
//      field, lifecycle status, validation status, ownership, audit metadata.
//   7. Persist via IRegistryEntityStore.UpdateAsync — on
//      RegistryConcurrencyConflictException emit the spec-006 ConflictResponse
//      via ConcurrencyConflictMapper (409).
//   8. Write `NamespaceMetadataUpdated` audit event with field diff.
//   9. Return 200 + new ETag header.
public static class UpdateMetadataEndpoint
{
    public static IEndpointRouteBuilder MapUpdateMetadataEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapPut("/api/namespaces/{id:guid}/metadata", HandleAsync)
            .RequireAuthorization()
            .RequireNamespaceAdministrator()
            .WithName("NamespaceMetadataUpdate")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid id,
        JsonElement body,
        IRegistryEntityStore entityStore,
        IAuditEventStore auditStore,
        UpdateMetadataValidator validator,
        ConcurrencyConflictMapper conflictMapper,
        NamespaceDtoMapping mapping,
        IPlatformPrincipalAccessor principalAccessor,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatchValues)
            || string.IsNullOrEmpty(ifMatchValues.ToString()))
        {
            return Problem(StatusCodes.Status428PreconditionRequired, "IfMatchRequired",
                "PUT requires the If-Match header carrying the entity's current ETag.",
                context.Request.Path);
        }
        var ifMatchEtag = ifMatchValues.ToString();

        UpdateMetadataRequest? request;
        try
        {
            request = body.Deserialize<UpdateMetadataRequest>(RegistryJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            return Problem(StatusCodes.Status400BadRequest, "MalformedJson", ex.Message, context.Request.Path);
        }

        if (request is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "EmptyBody",
                "Request body is required.", context.Request.Path);
        }

        request = request with { Id = id, RawBody = body };

        var current = await entityStore.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (current is not RegistryNamespace ns || ns.Source != RegistrySource.Onboarded)
        {
            return Problem(StatusCodes.Status404NotFound, "NotFound",
                $"No onboarded namespace with id {id:D} was found.", context.Request.Path);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                instance: context.Request.Path,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var now = timeProvider.GetUtcNow();
        var updatedEntity = ns with
        {
            UpdatedAtUtc = now,
            Description = request.Description,
            Tags = request.Tags ?? Array.Empty<RegistryTag>(),
        };
        updatedEntity = updatedEntity with
        {
            DisplayName = request.DisplayName,
            BusinessUnit = request.BusinessUnit,
            ProductOrApplication = request.ProductOrApplication,
            CostCenter = request.CostCenter,
            Notes = request.Notes,
        };

        RegistryEntity persisted;
        try
        {
            persisted = await entityStore.UpdateAsync(updatedEntity, ifMatchEtag, cancellationToken).ConfigureAwait(false);
        }
        catch (RegistryConcurrencyConflictException)
        {
            var fresh = await entityStore.FindByIdAsync(id, cancellationToken).ConfigureAwait(false) ?? ns;
            var conflict = conflictMapper.BuildConflict(fresh, updatedEntity, ifMatchEtag, context.Request.Path);
            return Results.Json(conflict, RegistryJsonOptions.Default,
                statusCode: StatusCodes.Status409Conflict,
                contentType: "application/problem+json");
        }

        var fieldChanges = RegistryAuditFactory.ComputeFieldChanges(ns, persisted);
        var summary = $"Updated metadata for namespace '{((RegistryNamespace)persisted).DisplayName ?? persisted.Name}'.";
        var audit = RegistryAuditFactory.Build(
            persisted,
            AuditEventType.NamespaceMetadataUpdated,
            summary,
            principalAccessor,
            timeProvider,
            fieldChanges: fieldChanges);
        await auditStore.WriteAsync(audit, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(persisted.Etag))
        {
            context.Response.Headers.ETag = persisted.Etag;
        }

        var response = mapping.ToResponse((RegistryNamespace)persisted);
        return Results.Json(response, statusCode: StatusCodes.Status200OK);
    }

    private static IResult Problem(int status, string code, string detail, string instance)
        => Results.Problem(
            title: code,
            detail: detail,
            statusCode: status,
            instance: instance,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = code,
            });
}
