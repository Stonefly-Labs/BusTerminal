using BusTerminal.Api.Features.Discovery.Shared.Domain;

namespace BusTerminal.Api.Features.Discovery.ListDiscoveryRuns;

// Spec 009 / T086 / US3. Response DTO for `GET /api/namespaces/{id}/discovery-runs`.
// Mirrors `DiscoveryRunPage` but is the on-the-wire contract — keeps the
// internal persistence model decoupled from the public surface.
public sealed record ListDiscoveryRunsResponse(
    IReadOnlyList<DiscoveryRun> Items,
    string? ContinuationToken);
