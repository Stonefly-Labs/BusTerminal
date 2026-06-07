using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-032..FR-034 / research §15 / contracts/audit-event.schema.json.
// One document per recorded change in the `registry-audit` Cosmos container
// (PK `/entityId`). Append-only from the user perspective; the API exposes no
// write surface on `/api/registry/{id}/audit`.
public sealed record AuditEvent(
    Guid Id,
    Guid EntityId,
    RegistryEntityType EntityType,
    string Environment,
    AuditEventType EventType,
    DateTimeOffset Timestamp,
    AuditActor Actor,
    string ChangeSummary,
    bool WasForceOverwrite,
    string CorrelationId,
    IReadOnlyList<AuditFieldChange>? FieldChanges = null);

// Spec 006 / contracts/audit-event.schema.json#eventType.
[JsonConverter(typeof(JsonStringEnumConverter<AuditEventType>))]
public enum AuditEventType
{
    Created,
    Updated,
    Deleted,
    StatusChanged,
}

// Spec 006 / data-model.md §5. `actor.displayName` carries the operator's
// preferred-name claim — permitted in audit storage (NOT in telemetry) per
// the PII boundary note.
public sealed record AuditActor(string PrincipalId, string DisplayName);

// Field-level diff entry. Present on Updated/StatusChanged; null on
// Created/Deleted. Values are opaque (any JSON-serializable shape).
public sealed record AuditFieldChange(string Field, object? Before, object? After);
