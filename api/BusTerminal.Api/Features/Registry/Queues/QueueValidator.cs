using BusTerminal.Api.Features.Registry.Shared;
using FluentValidation;

namespace BusTerminal.Api.Features.Registry.Queues;

// Spec 006 / T074 / data-model.md §3.2. Validates a Queue entity payload.
// Composes the shared rules with the Queue length cap (≤ 260 chars, base
// charset) and the parent-required shape (FR-008).
public sealed class QueueValidator : AbstractValidator<RegistryEntity>
{
    public QueueValidator()
    {
        RuleFor(x => x.Id).RequiredId();
        RuleFor(x => x.Name)
            .RequiredName()
            .BaseNameFormat()
            .MaxNameLengthFor(260, "Queue");
        RuleFor(x => x.Environment).RequiredEnvironment();
        RuleFor(x => x.Status).StatusValue();
        RuleFor(x => x.Source).SourceValue();
        RuleFor(x => x.EntityType)
            .Equal(RegistryEntityType.Queue)
            .WithMessage("entityType must be Queue for this endpoint.");
        RuleFor(x => x.ParentId)
            .NotNull()
            .Must(p => p is not null && p.Value != Guid.Empty)
            .WithMessage("Queue requires a parentId pointing to a Namespace.");
        RuleFor(x => x.Description).DescriptionLength();
        RuleFor(x => x.Owner).OwnerLength();
        RuleFor(x => x.AzureResourceId).AzureResourceIdFormat(RegistryEntityType.Queue);
        RuleFor(x => x.Tags).TagShape();
        RuleFor(x => x.Metadata).MetadataSize();
    }
}
