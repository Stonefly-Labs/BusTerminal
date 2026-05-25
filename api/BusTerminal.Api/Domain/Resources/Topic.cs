using System.Text.Json;

namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-005. Matches contracts/resources/topic.schema.json.
public sealed record Topic : Resource
{
    public required OrderingPolicy Ordering { get; init; }

    public bool Partitioned { get; init; }

    public IReadOnlyCollection<ResourceId> SubscriptionIds { get; init; } = [];

    public IReadOnlyCollection<ContractReference> ContractAssociations { get; init; } = [];

    public IReadOnlyCollection<ApplicationReference> Producers { get; init; } = [];

    public JsonElement? Governance { get; init; }
}
