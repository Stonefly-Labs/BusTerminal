namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §1.1 (onboardingActor). Immutable record of who
// performed the first registration. Captured at onboarding time and never
// rewritten — subsequent edits (metadata, ownership, lifecycle) are tracked
// in the audit log, not on this field.
public sealed record OnboardingActor(
    Guid ObjectId,
    string DisplayNameSnapshot,
    DateTimeOffset OnboardedAtUtc);
