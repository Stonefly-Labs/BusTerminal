using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §2 ValidationFailureCategory + §6 "Reason categories".
// PII-safe categorical bucket for downstream telemetry aggregation. Raw
// exception messages NEVER reach this enum — the runner maps exceptions to
// `Unknown` and emits a structured WARNING log with the full exception detail
// to App Insights so operators can correlate without leaking PII into spans.
[JsonConverter(typeof(JsonStringEnumConverter<ValidationFailureCategory>))]
public enum ValidationFailureCategory
{
    Ok,
    Timeout,
    Unauthorized,
    NotFound,
    Throttled,
    CrossTenant,
    Unknown,
}
