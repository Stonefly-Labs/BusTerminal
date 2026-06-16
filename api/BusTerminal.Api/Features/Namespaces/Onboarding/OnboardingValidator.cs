using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using FluentValidation;

namespace BusTerminal.Api.Features.Namespaces.Onboarding;

// Spec 008 / data-model.md §5 OnboardingRequest. Synchronous rules cover
// shape + length + ownership invariants. The async rules (ARM id verification,
// already-onboarded check, ValidationRun freshness) consume the
// IArmSubscriptionTenantResolver, IRegistryEntityStore, and
// INamespaceValidationRunStore ports via DI — they run during the
// OnboardingEndpoint's pre-persistence pipeline so the FR-023a hard-block
// (Unhealthy or stale validation rejection) happens before any document write.
public sealed class OnboardingValidator : AbstractValidator<OnboardingRequest>
{
    public const int MaxValidationRunAgeMinutes = 30;

    public OnboardingValidator(
        NamespaceArmIdParser armIdParser,
        IRegistryEntityStore entityStore,
        INamespaceValidationRunStore validationRunStore,
        TimeProvider timeProvider)
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("Namespace id is required and must be pre-allocated by the wizard.");

        RuleFor(x => x.AzureResourceId)
            .NotEmpty()
            .WithMessage("Azure Resource ID is required.");

        RuleFor(x => x.AzureResourceId)
            .MustAsync(async (armId, ct) =>
            {
                var parseResult = await armIdParser.ParseAndVerifyAsync(armId, ct);
                return parseResult.IsSuccess;
            })
            .WithMessage("Azure Resource ID failed parse or cross-tenant verification.")
            .When(x => !string.IsNullOrWhiteSpace(x.AzureResourceId));

        RuleFor(x => x.AzureResourceId)
            .MustAsync(async (armId, ct) =>
            {
                if (string.IsNullOrWhiteSpace(armId)) return true;
                var existing = await entityStore.FindByAzureResourceIdAsync(armId, ct).ConfigureAwait(false);
                return existing is null;
            })
            .WithMessage("This Azure Service Bus namespace is already onboarded.")
            .When(x => !string.IsNullOrWhiteSpace(x.AzureResourceId));

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.BusinessUnit).MaximumLength(200);
        RuleFor(x => x.ProductOrApplication).MaximumLength(200);
        RuleFor(x => x.CostCenter).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(4000);

        RuleFor(x => x.Environment)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Ownership)
            .NotNull().WithMessage("Ownership block is required.");

        RuleFor(x => x.Ownership.PrimaryOwner)
            .NotNull().WithMessage("Primary owner is required.")
            .When(x => x.Ownership is not null);

        RuleForEach(x => x.Ownership.SecondaryOwners)
            .ChildRules(o => o.RuleFor(a => a.DisplayNameSnapshot).NotEmpty().MaximumLength(256))
            .When(x => x.Ownership is not null);
        RuleForEach(x => x.Ownership.TechnicalStewards)
            .ChildRules(o => o.RuleFor(a => a.DisplayNameSnapshot).NotEmpty().MaximumLength(256))
            .When(x => x.Ownership is not null);
        RuleForEach(x => x.Ownership.SupportContacts)
            .ChildRules(o => o.RuleFor(a => a.DisplayNameSnapshot).NotEmpty().MaximumLength(256))
            .When(x => x.Ownership is not null);

        RuleFor(x => x)
            .MustAsync(async (request, ct) =>
            {
                var run = await validationRunStore
                    .GetAsync(request.Id, request.ValidationRunId, ct)
                    .ConfigureAwait(false);

                if (run is null) return false;
                if (run.AggregateStatus == ValidationStatus.Unhealthy) return false;

                var age = timeProvider.GetUtcNow() - run.ExecutedAtUtc;
                return age <= TimeSpan.FromMinutes(MaxValidationRunAgeMinutes);
            })
            .WithMessage(
                $"Validation run must be Healthy or Degraded AND less than {MaxValidationRunAgeMinutes} minutes old (FR-023a).")
            .WithName(nameof(OnboardingRequest.ValidationRunId));
    }
}
