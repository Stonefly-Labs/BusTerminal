namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-009 referent. Matches contracts/resources/team.schema.json.
// Teams own resources; teams themselves are owned organizationally (Ownership is null).
public sealed record Team : Resource
{
    public required string Slug { get; init; }

    public Guid? EntraGroupId { get; init; }

    public string? ContactEmail { get; init; }

    public OperationalTier? OperationalTier { get; init; }
}
