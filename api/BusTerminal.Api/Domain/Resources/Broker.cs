namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-002. Matches contracts/resources/broker.schema.json.
// BrokerKind is open-string ("AzureServiceBus" today; Kafka / RabbitMQ later)
// per Constitution Principle VI — Incremental Extensibility.
public sealed record Broker : Resource
{
    public required string BrokerKind { get; init; }

    public string? Endpoint { get; init; }

    public BrokerCapabilities? Capabilities { get; init; }
}

public sealed record BrokerCapabilities(
    bool Queues = false,
    bool Topics = false,
    bool Sessions = false,
    bool Transactions = false);
