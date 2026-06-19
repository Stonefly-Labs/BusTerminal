using System.Diagnostics;
using System.Text.Json;
using BusTerminal.Api.Authorization;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-032 / data-model.md §5. Helper that constructs an AuditEvent
// from the endpoint context. Kept in `_Shared` so every CRUD endpoint emits
// the same shape (id, actor, correlationId, timestamp) without copy-paste.
//
// Audit-vs-telemetry boundary (data-model.md §5): the actor.displayName field
// carries PII permitted only in the audit container, never in OTel/telemetry.
internal static class RegistryAuditFactory
{
    public const string DeprecatedParentPrefix = "UNDER_DEPRECATED_PARENT: ";

    public static AuditEvent Build(
        RegistryEntity entity,
        AuditEventType eventType,
        string changeSummary,
        IPlatformPrincipalAccessor principalAccessor,
        TimeProvider timeProvider,
        bool wasForceOverwrite = false,
        IReadOnlyList<AuditFieldChange>? fieldChanges = null,
        bool parentIsDeprecated = false)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(principalAccessor);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var principal = principalAccessor.Current;
        var actor = new AuditActor(
            PrincipalId: principal is null || principal.ObjectId == Guid.Empty
                ? string.Empty
                : principal.ObjectId.ToString("D"),
            DisplayName: principal?.DisplayName ?? string.Empty);

        // Spec 006 / FR-042 — correlationId connects audit events to OTel
        // traces in App Insights. In production with the AspNetCore OTel
        // instrumentation active (auto-wired by `UseAzureMonitor`), the
        // ambient Activity always carries a valid trace id. In test mode
        // (no AppInsights connection string → no auto-instrumentation) or
        // background-job paths without an inbound HTTP request,
        // `Activity.Current` can be null. Falling back to an empty string
        // was the source of a flaky audit-shape test and a defect for any
        // background-emitted audit event. A fresh Guid keeps audit events
        // self-correlatable (every event has a non-empty correlationId);
        // when an Activity IS present, real cross-system trace linkage
        // continues to work.
        var correlationId = Activity.Current?.TraceId.ToHexString()
            ?? Guid.NewGuid().ToString("N");

        var summary = parentIsDeprecated
            ? DeprecatedParentPrefix + changeSummary
            : changeSummary;

        return new AuditEvent(
            Id: Guid.NewGuid(),
            EntityId: entity.Id,
            EntityType: entity.EntityType,
            Environment: entity.Environment,
            EventType: eventType,
            Timestamp: timeProvider.GetUtcNow(),
            Actor: actor,
            ChangeSummary: summary,
            WasForceOverwrite: wasForceOverwrite,
            CorrelationId: correlationId,
            FieldChanges: fieldChanges);
    }

    // Compute field-level differences between two entities for the audit
    // fieldChanges array. Mirrors ConcurrencyConflictMapper's exclusion list
    // so server-managed timestamps don't drown the diff.
    public static IReadOnlyList<AuditFieldChange> ComputeFieldChanges(
        RegistryEntity before,
        RegistryEntity after)
    {
        var beforeJson = JsonSerializer.SerializeToElement(before, before.GetType(), RegistryJsonOptions.Default);
        var afterJson = JsonSerializer.SerializeToElement(after, after.GetType(), RegistryJsonOptions.Default);

        var changes = new List<AuditFieldChange>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (beforeJson.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in beforeJson.EnumerateObject()) names.Add(p.Name);
        }
        if (afterJson.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in afterJson.EnumerateObject()) names.Add(p.Name);
        }

        foreach (var name in names)
        {
            if (name is "_etag" or "etag" or "createdAtUtc" or "updatedAtUtc" or "fullyQualifiedName")
            {
                continue;
            }

            var hasBefore = beforeJson.TryGetProperty(name, out var b);
            var hasAfter = afterJson.TryGetProperty(name, out var a);

            var bText = hasBefore ? JsonSerializer.Serialize(b, RegistryJsonOptions.Default) : "null";
            var aText = hasAfter ? JsonSerializer.Serialize(a, RegistryJsonOptions.Default) : "null";
            if (!string.Equals(bText, aText, StringComparison.Ordinal))
            {
                changes.Add(new AuditFieldChange(
                    name,
                    hasBefore ? Unwrap(b) : null,
                    hasAfter ? Unwrap(a) : null));
            }
        }

        return changes;
    }

    private static object? Unwrap(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var i) ? i : e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Undefined => null,
        _ => e.Clone(),
    };
}
