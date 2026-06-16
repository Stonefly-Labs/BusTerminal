using BusTerminal.Api.Features.Namespaces.Shared;
using FluentValidation;

namespace BusTerminal.Api.Features.Namespaces.Ownership;

// Spec 008 / data-model.md §5 UpdateOwnershipRequest +
// FR-010 (exactly-one PrimaryOwner). Full-block replace; the endpoint maps a
// successful validation into a `with`-expression update on the persisted
// document.
public sealed class UpdateOwnershipValidator : AbstractValidator<UpdateOwnershipRequest>
{
    public UpdateOwnershipValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);

        RuleFor(x => x.Ownership)
            .NotNull().WithMessage("Ownership block is required.");

        RuleFor(x => x.Ownership.PrimaryOwner)
            .NotNull().WithMessage("Primary owner is required.")
            .When(x => x.Ownership is not null);

        RuleFor(x => x.Ownership.PrimaryOwner.ObjectId)
            .NotEqual(Guid.Empty).WithMessage("Primary owner objectId must be a valid Guid.")
            .When(x => x.Ownership?.PrimaryOwner is not null);

        RuleFor(x => x.Ownership.PrimaryOwner.Role)
            .Equal(OwnershipRole.PrimaryOwner)
            .WithMessage("Primary owner role discriminator must equal PrimaryOwner.")
            .When(x => x.Ownership?.PrimaryOwner is not null);

        RuleFor(x => x.Ownership)
            .Must(NoDuplicateAssignmentsWithinRole)
            .WithMessage("Ownership block must not contain duplicate (role, objectId) pairs within any role list.")
            .When(x => x.Ownership is not null);
    }

    private static bool NoDuplicateAssignmentsWithinRole(OwnershipBlock block)
    {
        if (block is null) return true;
        return NoDuplicates(block.SecondaryOwners, OwnershipRole.SecondaryOwner)
            && NoDuplicates(block.TechnicalStewards, OwnershipRole.TechnicalSteward)
            && NoDuplicates(block.SupportContacts, OwnershipRole.SupportContact);
    }

    private static bool NoDuplicates(IReadOnlyList<OwnershipAssignment> assignments, OwnershipRole expectedRole)
    {
        if (assignments is null || assignments.Count == 0) return true;
        var seen = new HashSet<Guid>();
        foreach (var a in assignments)
        {
            if (a.Role != expectedRole) return false;
            if (a.ObjectId == Guid.Empty) return false;
            if (!seen.Add(a.ObjectId)) return false;
        }
        return true;
    }
}
