namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-002. Matches contracts/resources/producer-application.schema.json.
public sealed record ProducerApplication : Resource
{
    public required string ApplicationKind { get; init; }

    public string? Repository { get; init; }

    public string? OnCallReference { get; init; }
}
