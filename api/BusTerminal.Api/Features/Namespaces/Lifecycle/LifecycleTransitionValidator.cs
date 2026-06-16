using BusTerminal.Api.Features.Namespaces.Shared;
using FluentValidation;

namespace BusTerminal.Api.Features.Namespaces.Lifecycle;

// Spec 008 / data-model.md §5 LifecycleTransitionRequest + §2 LifecycleStatus
// transition table.
//   Active ⇄ Disabled
//   Active | Disabled → Archived
//   Archived → Disabled (restore)
// Endpoint validators feed `current` lifecycle status alongside the request
// shape; transitions outside the table return 400 with a reason category.
public sealed class LifecycleTransitionValidator : AbstractValidator<LifecycleTransitionValidationInput>
{
    public LifecycleTransitionValidator()
    {
        RuleFor(x => x.Request.Id).NotEqual(Guid.Empty);

        RuleFor(x => x.Request.Action)
            .IsInEnum();

        RuleFor(x => x.Request.Reason)
            .NotEmpty()
            .WithMessage("A reason is required for disable, archive, and restore actions.")
            .When(x => x.Request.Action is LifecycleAction.Disable or LifecycleAction.Archive or LifecycleAction.Restore);

        RuleFor(x => x.Request.Reason)
            .MaximumLength(1000);

        RuleFor(x => x)
            .Must(IsTransitionPermitted)
            .WithMessage(input =>
                $"Lifecycle transition from {input.CurrentStatus} via {input.Request.Action} is not permitted by FR-023.")
            .WithName(nameof(LifecycleTransitionRequest.Action));
    }

    private static bool IsTransitionPermitted(LifecycleTransitionValidationInput input)
    {
        return (input.CurrentStatus, input.Request.Action) switch
        {
            (LifecycleStatus.Active, LifecycleAction.Disable) => true,
            (LifecycleStatus.Disabled, LifecycleAction.Enable) => true,
            (LifecycleStatus.Active, LifecycleAction.Archive) => true,
            (LifecycleStatus.Disabled, LifecycleAction.Archive) => true,
            (LifecycleStatus.Archived, LifecycleAction.Restore) => true,
            _ => false,
        };
    }
}

// Wrapper carries the request + the current persisted lifecycle status so the
// validator can enforce the transition table.
public sealed record LifecycleTransitionValidationInput(
    LifecycleTransitionRequest Request,
    LifecycleStatus CurrentStatus);
