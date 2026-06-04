using System.Text.Json;
using BusTerminal.Api.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T081 / FR-013a. PATCH /api/registry/{id}/status — flip between
// Active and Deprecated. Writes a `StatusChanged` audit event (or a generic
// `Updated` with no-op semantics when the value didn't change — same-status
// writes are accepted but produce no event per data-model.md §3.2).
public static class StatusEndpoint
{
    public static RouteGroupBuilder MapStatusChangeEndpoint(this RouteGroupBuilder group)
    {
        group.MapPatch("/{id:guid}/status", HandleAsync).WithName("RegistryStatusChange");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid id,
        StatusChangeRequest body,
        IRegistryEntityStore store,
        IAuditEventStore auditStore,
        IPlatformPrincipalAccessor principalAccessor,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatchValues)
            || string.IsNullOrEmpty(ifMatchValues.ToString()))
        {
            return RegistryProblemFactory.PreconditionRequired(
                "PATCH /status requires the If-Match header carrying the entity's current ETag.",
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

        if (current.Status == body.Status)
        {
            // No-op write — return the current entity unchanged, no audit emitted.
            if (!string.IsNullOrEmpty(current.Etag))
            {
                context.Response.Headers.ETag = current.Etag;
            }
            return Results.Json(current, RegistryJsonOptions.Default, statusCode: 200);
        }

        var now = timeProvider.GetUtcNow();
        var updated = current with { Status = body.Status, UpdatedAtUtc = now };

        RegistryEntity saved;
        try
        {
            saved = await store.UpdateAsync(updated, ifMatchEtag, cancellationToken).ConfigureAwait(false);
        }
        catch (RegistryConcurrencyConflictException)
        {
            return RegistryProblemFactory.Conflict(
                "ConcurrencyConflict",
                "Concurrency conflict",
                "The entity was modified by another writer since you loaded it.",
                context.Request.Path);
        }

        var fieldChanges = new List<AuditFieldChange>
        {
            new("status", current.Status.ToString(), saved.Status.ToString()),
        };
        var summary = $"Status changed for {saved.EntityType} '{saved.Name}': {current.Status} → {saved.Status}";
        var audit = RegistryAuditFactory.Build(
            saved, AuditEventType.StatusChanged, summary,
            principalAccessor, timeProvider,
            fieldChanges: fieldChanges);
        await auditStore.WriteAsync(audit, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(saved.Etag))
        {
            context.Response.Headers.ETag = saved.Etag;
        }
        return Results.Json(saved, RegistryJsonOptions.Default, statusCode: 200);
    }
}
