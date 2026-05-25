using System.Text.Json;

namespace BusTerminal.Api.Domain.Lifecycle;

// Spec 004 / FR-015 / Q5. Matches contracts/change-event.schema.json.
// Persisted in the `change-events` Cosmos container, partitioned by /resourceId.
// Append-only — never updated, never deleted in v1.
public sealed record ChangeEvent(
    Guid Id,
    ResourceId ResourceId,
    string ResourceType,
    ChangeEventType EventType,
    PrincipalReference Actor,
    DateTimeOffset Timestamp,
    ConcurrencyToken ConcurrencyTokenAfter,
    ConcurrencyToken? ConcurrencyTokenBefore = null,
    LifecycleState? LifecycleBefore = null,
    LifecycleState? LifecycleAfter = null,
    string? SourceSystem = null,
    JsonElement? Diff = null,
    JsonElement? Snapshot = null);
