using System.Text.Json;
using BusTerminal.Api.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T078 / FR-020. PUT /api/registry/{id}.
//   - Requires `If-Match: <etag>` header.
//   - On stale ETag → 409 ConflictResponse via ConcurrencyConflictMapper.
//   - On `_overwriteAcknowledged: true` without conflict → 400
//     `ForceOverwriteWithoutConflict` (prevents flag-stuffing per data-model §3.3).
//   - On Updated → 200 + new ETag; emits Updated audit event with field diff.
public static class UpdateEndpoint
{
    public static RouteGroupBuilder MapUpdateRegistryEntity(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", HandleAsync).WithName("RegistryUpdate");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid id,
        JsonElement body,
        IRegistryEntityStore store,
        IAuditEventStore auditStore,
        RegistryValidatorDispatcher validators,
        RegistryDtoMapping mapping,
        ConcurrencyConflictMapper conflictMapper,
        IPlatformPrincipalAccessor principalAccessor,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatchValues)
            || string.IsNullOrEmpty(ifMatchValues.ToString()))
        {
            return RegistryProblemFactory.PreconditionRequired(
                "PUT requires the If-Match header carrying the entity's current ETag.",
                context.Request.Path);
        }
        var ifMatchEtag = ifMatchValues.ToString();

        UpdateEntityRequest? request;
        try
        {
            request = body.Deserialize<UpdateEntityRequest>(RegistryJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            return RegistryProblemFactory.BadRequest(
                "MalformedJson", "Malformed JSON body", ex.Message, context.Request.Path);
        }

        if (request is null)
        {
            return RegistryProblemFactory.BadRequest(
                "EmptyBody", "Empty body", "Request body is required.", context.Request.Path);
        }

        if (request.Id != id)
        {
            return RegistryProblemFactory.BadRequest(
                "IdMismatch", "id mismatch",
                "The id in the request body must match the URL route value.",
                context.Request.Path);
        }

        // Load current entity for invariants + diff + parent resolution.
        var current = await store.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return RegistryProblemFactory.NotFound(
                "NotFound", "Entity not found",
                $"No registry entity with id {id} was found.",
                context.Request.Path);
        }

        // Spec 008 / research §7. Onboarded-source namespaces are owned by the
        // /api/namespaces surface; the spec-006 polymorphic PUT does not carry
        // the structured ownership block and would obliterate spec-008 fields.
        // Reject with a 409 that redirects the caller to the typed endpoint.
        if (current.Source == RegistrySource.Onboarded)
        {
            return RegistryProblemFactory.Conflict(
                "OnboardedNamespaceWriteNotPermitted",
                "Onboarded namespace writes are not permitted on /api/registry",
                "This namespace was onboarded via the spec-008 wizard and is read-only through the polymorphic registry surface. Use /api/namespaces/{id}/metadata for metadata edits or /api/namespaces/{id}/ownership for ownership edits.",
                $"/api/namespaces/{current.Id:D}/metadata");
        }

        // Immutables: entityType, createdAtUtc, environment, source.
        if (request.EntityType != current.EntityType)
        {
            return RegistryProblemFactory.BadRequest(
                "EntityTypeImmutable", "entityType is immutable",
                "entityType cannot change after first save.", context.Request.Path);
        }
        if (!string.Equals(request.Environment, current.Environment, StringComparison.Ordinal))
        {
            return RegistryProblemFactory.BadRequest(
                "EnvironmentImmutable", "environment is immutable",
                "environment cannot change after first save.", context.Request.Path);
        }

        // Parent resolution for FQN.
        RegistryEntity? parent = null;
        if (request.ParentId.HasValue)
        {
            var expectedParent = CreateEndpoint.ExpectedParentType(request.EntityType);
            if (expectedParent is null)
            {
                return RegistryProblemFactory.BadRequest(
                    "InvalidParent", "Invalid parent reference",
                    $"{request.EntityType} entities must not declare a parentId.",
                    context.Request.Path);
            }
            parent = await store.FindParentAsync(
                request.ParentId.Value, expectedParent.Value, request.Environment, cancellationToken)
                .ConfigureAwait(false);
            if (parent is null)
            {
                return RegistryProblemFactory.BadRequest(
                    "ParentNotFound", "Parent not found",
                    $"Parent of expected type {expectedParent.Value} with id {request.ParentId.Value} was not found in environment '{request.Environment}'.",
                    context.Request.Path);
            }
        }

        var fqn = await CreateEndpoint.ComputeFqnAsync(
            store, request.EntityType, request.Name, parent, request.Environment, cancellationToken)
            .ConfigureAwait(false);
        var namespaceName = request.EntityType == RegistryEntityType.Namespace
            ? request.Name
            : (parent?.EntityType == RegistryEntityType.Namespace ? parent.Name : current.NamespaceName);

        var now = timeProvider.GetUtcNow();
        var entity = EntityMaterializer.FromUpdateRequest(request, current, now, fqn, namespaceName);

        var tags = mapping.NormalizeTags(entity.Tags, current.Tags);
        entity = entity with { Tags = tags };

        var validation = await validators.ValidateAsync(entity, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return RegistryProblemFactory.ValidationProblem(validation, context.Request.Path);
        }

        // If `_overwriteAcknowledged: true` is sent BUT the If-Match still
        // matches the current etag, there is no conflict to overwrite —
        // reject so the operator can't stuff the flag (data-model §3.3).
        var overwriteAcknowledged = mapping.ExtractOverwriteAcknowledged(body);
        if (overwriteAcknowledged
            && string.Equals(ifMatchEtag, current.Etag, StringComparison.Ordinal))
        {
            return RegistryProblemFactory.BadRequest(
                "ForceOverwriteWithoutConflict",
                "Force-overwrite without a detected conflict",
                "_overwriteAcknowledged=true is only valid when responding to an actual 409 ConcurrencyConflict.",
                context.Request.Path);
        }

        // Duplicate name (FR-014). When name or parent changes, check siblings.
        if (!string.Equals(current.Name, entity.Name, StringComparison.Ordinal)
            || current.ParentId != entity.ParentId)
        {
            var duplicate = await store.FindByParentAndNameAsync(
                entity.ParentId, entity.EntityType, entity.Name, entity.Environment, cancellationToken)
                .ConfigureAwait(false);
            if (duplicate is not null && duplicate.Id != entity.Id)
            {
                return RegistryProblemFactory.Conflict(
                    "DuplicateName",
                    "Duplicate name within parent scope",
                    $"An entity of type {entity.EntityType} named '{entity.Name}' already exists under parent {entity.ParentId} in environment '{entity.Environment}'.",
                    context.Request.Path);
            }
        }

        RegistryEntity updated;
        try
        {
            updated = await store.UpdateAsync(entity, ifMatchEtag, cancellationToken).ConfigureAwait(false);
        }
        catch (RegistryConcurrencyConflictException)
        {
            // Re-load the current state for the diff. The persisted entity may
            // have moved on since we read it above.
            var freshCurrent = await store.FindByIdAsync(id, cancellationToken).ConfigureAwait(false)
                ?? current;
            var conflict = conflictMapper.BuildConflict(
                currentEntity: freshCurrent,
                submittedEntity: entity,
                submittedEtag: ifMatchEtag,
                instance: context.Request.Path);
            return Results.Json(conflict, RegistryJsonOptions.Default, statusCode: 409,
                contentType: RegistryProblemFactory.ProblemContentType);
        }

        var fieldChanges = RegistryAuditFactory.ComputeFieldChanges(current, updated);
        var summary = updated.Status != current.Status
            ? $"Updated {updated.EntityType} '{updated.Name}' (status: {current.Status} → {updated.Status})"
            : $"Updated {updated.EntityType} '{updated.Name}'";
        var auditEvent = RegistryAuditFactory.Build(
            updated,
            updated.Status != current.Status ? AuditEventType.StatusChanged : AuditEventType.Updated,
            summary,
            principalAccessor, timeProvider,
            wasForceOverwrite: overwriteAcknowledged,
            fieldChanges: fieldChanges);
        await auditStore.WriteAsync(auditEvent, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(updated.Etag))
        {
            context.Response.Headers.ETag = updated.Etag;
        }
        return Results.Json(updated, RegistryJsonOptions.Default, statusCode: 200);
    }
}
