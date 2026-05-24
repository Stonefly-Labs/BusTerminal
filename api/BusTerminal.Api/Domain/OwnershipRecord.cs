using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-009. Matches contracts/ownership.schema.json.
// ContactReference is forward-compatible: today freeform values are common,
// future Graph integration slice promotes them to Entra references in place.

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(EntraContactReference), "entra")]
[JsonDerivedType(typeof(FreeformContactReference), "freeform")]
public abstract record ContactReference;

public sealed record EntraContactReference(Guid ObjectId) : ContactReference;

public sealed record FreeformContactReference(string Value) : ContactReference;

[JsonConverter(typeof(JsonStringEnumConverter<OperationalTier>))]
public enum OperationalTier
{
    Tier1,
    Tier2,
    Tier3,
    BestEffort,
}

public sealed record OwnershipRecord(
    ResourceId OwningTeamId,
    OperationalTier OperationalTier,
    ContactReference? TechnicalContact = null,
    ContactReference? BusinessContact = null,
    string? EscalationReference = null,
    string? SupportReference = null);
