namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-032..FR-034 / data-model.md §5. Append-only audit log.
// Writes are fire-and-forget from the entity write's perspective — a failed
// audit append logs an error but does NOT roll back the entity write
// (data-model.md §5 "Audit events are written after a successful entity
// write …").
public interface IAuditEventStore
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    // Entity-scoped read for the audit panel (FR-033). Newest-first ordering;
    // `limit` is clamped to 1..200 (research §13).
    Task<IReadOnlyList<AuditEvent>> ListForEntityAsync(
        Guid entityId,
        int limit,
        CancellationToken cancellationToken);
}
