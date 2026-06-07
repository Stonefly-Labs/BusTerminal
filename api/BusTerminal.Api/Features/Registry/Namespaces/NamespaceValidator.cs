using BusTerminal.Api.Features.Registry.Shared;
using FluentValidation;

namespace BusTerminal.Api.Features.Registry.Namespaces;

// Spec 006 / T074 / data-model.md §3.2. Validates a Namespace entity payload
// (create or update). Composes the shared canonical-field rules with the
// Namespace name specialization (6–50 chars, must start with a letter, end
// alphanumeric, hyphens only inside) and the parent=null shape.
public sealed class NamespaceValidator : AbstractValidator<RegistryEntity>
{
    public NamespaceValidator()
    {
        RuleFor(x => x.Id).RequiredId();
        RuleFor(x => x.Name).RequiredName().NamespaceNameFormat();
        RuleFor(x => x.Environment).RequiredEnvironment();
        RuleFor(x => x.Status).StatusValue();
        RuleFor(x => x.Source).SourceValue();
        RuleFor(x => x.EntityType)
            .Equal(RegistryEntityType.Namespace)
            .WithMessage("entityType must be Namespace for this endpoint.");
        RuleFor(x => x.ParentId)
            .Must(p => p is null)
            .WithMessage("Namespace entities must not declare a parentId.");
        RuleFor(x => x.Description).DescriptionLength();
        RuleFor(x => x.Owner).OwnerLength();
        RuleFor(x => x.AzureResourceId).AzureResourceIdFormat(RegistryEntityType.Namespace);
        RuleFor(x => x.Tags).TagShape();
        RuleFor(x => x.Metadata).MetadataSize();
    }
}
