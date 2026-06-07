using BusTerminal.Api.Features.Registry.Shared;
using FluentValidation;

namespace BusTerminal.Api.Features.Registry.Rules;

// Spec 006 / T074 / data-model.md §3.2. Validates a Rule entity payload.
// Composes the shared rules with the Rule length cap (≤ 50 chars) and the
// parent-required shape (parent is a Subscription).
public sealed class RuleValidator : AbstractValidator<RegistryEntity>
{
    public RuleValidator()
    {
        RuleFor(x => x.Id).RequiredId();
        RuleFor(x => x.Name)
            .RequiredName()
            .BaseNameFormat()
            .MaxNameLengthFor(50, "Rule");
        RuleFor(x => x.Environment).RequiredEnvironment();
        RuleFor(x => x.Status).StatusValue();
        RuleFor(x => x.Source).SourceValue();
        RuleFor(x => x.EntityType)
            .Equal(RegistryEntityType.Rule)
            .WithMessage("entityType must be Rule for this endpoint.");
        RuleFor(x => x.ParentId)
            .NotNull()
            .Must(p => p is not null && p.Value != Guid.Empty)
            .WithMessage("Rule requires a parentId pointing to a Subscription.");
        RuleFor(x => x.Description).DescriptionLength();
        RuleFor(x => x.Owner).OwnerLength();
        RuleFor(x => x.AzureResourceId).AzureResourceIdFormat(RegistryEntityType.Rule);
        RuleFor(x => x.Tags).TagShape();
        RuleFor(x => x.Metadata).MetadataSize();
    }
}
