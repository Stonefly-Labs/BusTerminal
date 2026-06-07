using BusTerminal.Api.Features.Registry.Shared;
using FluentValidation;

namespace BusTerminal.Api.Features.Registry.Subscriptions;

// Spec 006 / T074 / data-model.md §3.2. Validates a Subscription entity
// payload. Composes the shared rules with the Subscription length cap
// (≤ 50 chars) and the parent-required shape (parent is a Topic).
public sealed class SubscriptionValidator : AbstractValidator<RegistryEntity>
{
    public SubscriptionValidator()
    {
        RuleFor(x => x.Id).RequiredId();
        RuleFor(x => x.Name)
            .RequiredName()
            .BaseNameFormat()
            .MaxNameLengthFor(50, "Subscription");
        RuleFor(x => x.Environment).RequiredEnvironment();
        RuleFor(x => x.Status).StatusValue();
        RuleFor(x => x.Source).SourceValue();
        RuleFor(x => x.EntityType)
            .Equal(RegistryEntityType.Subscription)
            .WithMessage("entityType must be Subscription for this endpoint.");
        RuleFor(x => x.ParentId)
            .NotNull()
            .Must(p => p is not null && p.Value != Guid.Empty)
            .WithMessage("Subscription requires a parentId pointing to a Topic.");
        RuleFor(x => x.Description).DescriptionLength();
        RuleFor(x => x.Owner).OwnerLength();
        RuleFor(x => x.AzureResourceId).AzureResourceIdFormat(RegistryEntityType.Subscription);
        RuleFor(x => x.Tags).TagShape();
        RuleFor(x => x.Metadata).MetadataSize();
    }
}
