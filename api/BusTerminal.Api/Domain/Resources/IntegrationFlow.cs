namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-002. Matches contracts/resources/integration-flow.schema.json.
public sealed record IntegrationFlow : Resource
{
    public required ResourceId ProducerApplicationId { get; init; }

    public required ResourceId MessagingResourceId { get; init; }

    public required IReadOnlyCollection<ResourceId> ConsumerApplicationIds { get; init; }

    public string? BusinessPurpose { get; init; }
}
