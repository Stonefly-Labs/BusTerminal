using BusTerminal.Api.Features.Registry.Shared;
using FluentValidation;

namespace BusTerminal.Api.Features.Registry.Topics;

// Spec 006 / T074 / data-model.md §3.2. Validates a Topic entity payload.
// Composes the shared rules with the Topic length cap (≤ 260 chars) and the
// parent-required shape (FR-008 — parent is a Namespace).
public sealed class TopicValidator : AbstractValidator<RegistryEntity>
{
    public TopicValidator()
    {
        RuleFor(x => x.Id).RequiredId();
        RuleFor(x => x.Name)
            .RequiredName()
            .BaseNameFormat()
            .MaxNameLengthFor(260, "Topic");
        RuleFor(x => x.Environment).RequiredEnvironment();
        RuleFor(x => x.Status).StatusValue();
        RuleFor(x => x.Source).SourceValue();
        RuleFor(x => x.EntityType)
            .Equal(RegistryEntityType.Topic)
            .WithMessage("entityType must be Topic for this endpoint.");
        RuleFor(x => x.ParentId)
            .NotNull()
            .Must(p => p is not null && p.Value != Guid.Empty)
            .WithMessage("Topic requires a parentId pointing to a Namespace.");
        RuleFor(x => x.Description).DescriptionLength();
        RuleFor(x => x.Owner).OwnerLength();
        RuleFor(x => x.AzureResourceId).AzureResourceIdFormat(RegistryEntityType.Topic);
        RuleFor(x => x.Tags).TagShape();
        RuleFor(x => x.Metadata).MetadataSize();
    }
}
