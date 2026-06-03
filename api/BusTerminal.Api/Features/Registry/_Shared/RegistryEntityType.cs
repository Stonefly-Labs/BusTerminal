using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-001 / data-model.md §1. Closed enum discriminator.
// Per data-model.md §10 "Forward compatibility" the wire string is the
// authoritative value across persisted JSON, OpenAPI, the AI Search index, and
// OTel attributes — enum names match exactly so a future broker (Kafka, etc.)
// adds a member without rewriting projections. PascalCase is fixed by the
// schema's `enum: ["Namespace", ...]` — JsonStringEnumConverter<T> with no
// naming policy emits enum names as-written.
[JsonConverter(typeof(JsonStringEnumConverter<RegistryEntityType>))]
public enum RegistryEntityType
{
    Namespace,
    Queue,
    Topic,
    Subscription,
    Rule,
}
