using System.Collections.Generic;
using System.Threading;
using BusTerminal.Indexer.Discovery.Classification;

namespace BusTerminal.Indexer.Discovery.Persistence;

// Spec 009 / T052 + FR-016 + R-08. Worker-side writer port. The single
// `UpsertAzureSourcedAsync` method is the only write the discovery
// pipeline emits — curated metadata stays untouched per FR-016.
public interface IPublishedEntityWriter
{
    Task<UpsertOutcome> UpsertAzureSourcedAsync(
        DiscoveryUpsert upsert,
        CancellationToken cancellationToken);

    // Missing sweep — yields entities the run did NOT observe so the
    // orchestrator can transition them to Missing.
    IAsyncEnumerable<MissingCandidate> ListMissingCandidatesAsync(
        string namespaceId,
        string environment,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken);

    Task TransitionToMissingAsync(
        string entityId,
        string environment,
        string ifMatch,
        DateTimeOffset whenUtc,
        string runId,
        CancellationToken cancellationToken);
}

public sealed record DiscoveryUpsert(
    string EntityId,
    string Environment,
    DiscoveredEntityType EntityType,
    string NamespaceId,
    string Name,
    string CompositeKey,
    string? ParentEntityId,
    IReadOnlyDictionary<string, object?> AzureSourced,
    string AzureSourcedHash,
    string DiscoveryRunId,
    DateTimeOffset DiscoveryRunStartedUtc,
    string DiscoveredBy);

public sealed record UpsertOutcome(ClassificationOutcome Outcome, double RuConsumed);

public sealed record MissingCandidate(
    string EntityId,
    string Environment,
    string ETag);
