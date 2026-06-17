namespace BusTerminal.Api.Features.Discovery.Shared.Domain;

// Spec 009 / FR-003 / data-model.md §1.2 — appended to a DiscoveryRun when
// the API coalesces a fresh request onto an in-flight run.
public sealed record CoalescedRequest(
    DateTimeOffset RequestedUtc,
    string RequestedBy);
