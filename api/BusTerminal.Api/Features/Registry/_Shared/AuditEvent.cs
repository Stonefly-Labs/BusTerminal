using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-032..FR-034 / research §15 / contracts/audit-event.schema.json
// + Spec 008 / data-model.md §1.4. One document per recorded change in the
// `registry-audit` Cosmos container (PK `/entityId`). Append-only from the
// user perspective; the API exposes no write surface on `/api/registry/{id}/audit`.
//
// LifecycleReason is populated only on NamespaceLifecycleTransitioned events
// (spec 008); null on every other event type.
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
    IReadOnlyList<AuditFieldChange>? FieldChanges = null,
    string? LifecycleReason = null);

// Spec 006 / contracts/audit-event.schema.json#eventType +
// Spec 008 / contracts/namespace-audit-event.schema.json. Spec-008 event types
// are emitted for Onboarded-source namespaces; spec-006 types remain for
// Manual-source documents and non-namespace entities.
[JsonConverter(typeof(JsonStringEnumConverter<AuditEventType>))]
public enum AuditEventType
{
    Created,
    Updated,
    Deleted,
    StatusChanged,
    NamespaceOnboarded,
    NamespaceMetadataUpdated,
    NamespaceOwnershipUpdated,
    NamespaceLifecycleTransitioned,
    NamespaceValidationExecuted,
}

// Spec 006 / data-model.md §5. `actor.displayName` carries the operator's
// preferred-name claim — permitted in audit storage (NOT in telemetry) per
// the PII boundary note.
public sealed record AuditActor(string PrincipalId, string DisplayName);

// Field-level diff entry. Present on Updated/StatusChanged; null on
// Created/Deleted. Values are opaque (any JSON-serializable shape).
public sealed record AuditFieldChange(string Field, object? Before, object? After);
