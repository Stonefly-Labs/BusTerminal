using BusTerminal.Api.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T079 / FR-013 + FR-009. DELETE /api/registry/{id}.
//   - Hard delete; block when children exist (HasChildrenResponse).
//   - Optimistic concurrency via If-Match.
//   - Tombstone-then-delete is implemented at the persistence layer
//     (CosmosRegistryEntityStore.DeleteAsync). Indexer (Phase 2) picks the
//     tombstone up via change feed and deletes the AI Search document.
public static class DeleteEndpoint
{
    public static RouteGroupBuilder MapDeleteRegistryEntity(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", HandleAsync).WithName("RegistryDelete");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid id,
        IRegistryEntityStore store,
        IAuditEventStore auditStore,
        ChildCountChecker childCountChecker,
        IPlatformPrincipalAccessor principalAccessor,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatchValues)
            || string.IsNullOrEmpty(ifMatchValues.ToString()))
        {
            return RegistryProblemFactory.PreconditionRequired(
                "DELETE requires the If-Match header carrying the entity's current ETag.",
                context.Request.Path);
        }
        var ifMatchEtag = ifMatchValues.ToString();

        var current = await store.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return RegistryProblemFactory.NotFound(
                "NotFound", "Entity not found",
                $"No registry entity with id {id} was found.",
                context.Request.Path);
        }

        // Spec 008 / FR-026 + research §7. Onboarded-source namespaces have no
        // physical-delete surface. Direct callers MUST use the lifecycle Archive
        // action instead so the audit trail remains intact.
        if (current.Source == RegistrySource.Onboarded)
        {
            return RegistryProblemFactory.Conflict(
                "OnboardedNamespaceDeleteNotPermitted",
                "Onboarded namespaces cannot be physically deleted",
                "This namespace was onboarded via the spec-008 wizard. Use POST /api/namespaces/{id}/lifecycle with action=archive to remove it from active use.",
                $"/api/namespaces/{current.Id:D}/lifecycle");
        }

        // FR-009 — block when children exist.
        var blockingChildren = await childCountChecker.CheckAsync(
            current.Id, current.Environment, context.Request.Path, cancellationToken)
            .ConfigureAwait(false);
        if (blockingChildren is not null)
        {
            return Results.Json(blockingChildren, RegistryJsonOptions.Default, statusCode: 409,
                contentType: RegistryProblemFactory.ProblemContentType);
        }

        try
        {
            await store.DeleteAsync(current.Id, current.Environment, ifMatchEtag, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RegistryConcurrencyConflictException)
        {
            return RegistryProblemFactory.Conflict(
                "ConcurrencyConflict",
                "Concurrency conflict",
                "The entity was modified by another writer since you loaded it.",
                context.Request.Path);
        }

        var summary = $"Deleted {current.EntityType} '{current.Name}'";
        var audit = RegistryAuditFactory.Build(
            current, AuditEventType.Deleted, summary,
            principalAccessor, timeProvider);
        await auditStore.WriteAsync(audit, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }
}
