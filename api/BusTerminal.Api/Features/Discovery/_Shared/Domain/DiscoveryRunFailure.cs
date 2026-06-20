namespace BusTerminal.Api.Features.Discovery.Shared.Domain;

// Spec 009 / data-model.md §1.2. Populated on DiscoveryRun.Failure only when
// status = Failed. Message is operator-safe — the worker sanitizes ARM IDs
// and entity names via FailureMessageSanitizer (Phase 3 T053a) before
// persistence.
public sealed record DiscoveryRunFailure(
    DiscoveryFailureCategory Category,
    string Message,
    DiscoveryPhase OccurredAtPhase,
    int? RetriesExhausted);
