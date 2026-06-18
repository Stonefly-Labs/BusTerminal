using System.Text.Json.Serialization;
using BusTerminal.Api.Features.Discovery.Shared.Domain;

namespace BusTerminal.Api.Features.Discovery.StartDiscovery;

// Spec 009 / T044 + contracts/openapi.yaml#StartDiscoveryResponse.
// FR-003 surfaces `CoalescedFromExisting=true` when the request was attached
// to an in-flight run instead of starting a new one. The 202-Accepted body
// is camel-cased on the wire by Program.cs' default System.Text.Json config.
// Enum status is serialized as the enum name (string) per the openapi.yaml
// contract — JsonStringEnumConverter pinned on the property keeps the
// serialization stable regardless of global config evolution.
public sealed record StartDiscoveryResponse(
    [property: JsonPropertyName("discoveryRunId")] string DiscoveryRunId,
    [property: JsonPropertyName("namespaceId")] string NamespaceId,
    [property: JsonPropertyName("status")]
    [property: JsonConverter(typeof(JsonStringEnumConverter<DiscoveryRunStatus>))]
    DiscoveryRunStatus Status,
    [property: JsonPropertyName("coalescedFromExisting")] bool CoalescedFromExisting,
    [property: JsonPropertyName("startedUtc")] DateTimeOffset StartedUtc);
