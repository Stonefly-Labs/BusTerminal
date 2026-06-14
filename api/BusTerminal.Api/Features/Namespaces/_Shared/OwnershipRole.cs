using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §2 OwnershipRole. The role discriminator on an
// OwnershipAssignment MUST match the containing OwnershipBlock slot per
// FR-010. Exactly one PrimaryOwner; zero-or-more of the other roles.
[JsonConverter(typeof(JsonStringEnumConverter<OwnershipRole>))]
public enum OwnershipRole
{
    PrimaryOwner,
    SecondaryOwner,
    TechnicalSteward,
    SupportContact,
}
