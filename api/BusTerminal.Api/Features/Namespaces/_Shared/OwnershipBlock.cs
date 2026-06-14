namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §1.3 OwnershipBlock. `PrimaryOwner` is required
// (FR-010); the other role lists default to empty arrays. The schema's
// `OwnershipBlock` $def uses camelCase property names (`primaryOwner`,
// `secondaryOwners`, `technicalStewards`, `supportContacts`) — System.Text.Json
// picks them up via the registry's project-wide CamelCase naming policy
// (RegistryJsonOptions.Default).
public sealed record OwnershipBlock(
    OwnershipAssignment PrimaryOwner,
    IReadOnlyList<OwnershipAssignment>? SecondaryOwners = null,
    IReadOnlyList<OwnershipAssignment>? TechnicalStewards = null,
    IReadOnlyList<OwnershipAssignment>? SupportContacts = null)
{
    public IReadOnlyList<OwnershipAssignment> SecondaryOwners { get; init; }
        = SecondaryOwners ?? Array.Empty<OwnershipAssignment>();

    public IReadOnlyList<OwnershipAssignment> TechnicalStewards { get; init; }
        = TechnicalStewards ?? Array.Empty<OwnershipAssignment>();

    public IReadOnlyList<OwnershipAssignment> SupportContacts { get; init; }
        = SupportContacts ?? Array.Empty<OwnershipAssignment>();
}
