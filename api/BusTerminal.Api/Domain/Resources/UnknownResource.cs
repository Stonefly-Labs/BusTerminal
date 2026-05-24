using System.Text.Json;

namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / Q4. Materialized when persisted `resourceType` is NOT in the
// ResourceTypeRegistry. Carries the raw JSON document so diagnostic tooling
// and the additive-evolution guard test (T158) can surface the original.
//
// Q4 guarantees: existing documents are never migrated, never modified, never
// rejected when their type is unknown to the current build — they simply
// materialize as this placeholder + emit an Info finding via
// UnknownResourceTypeRule.
public sealed record UnknownResource : Resource
{
    public required JsonElement RawJson { get; init; }
}
