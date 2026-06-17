using System.Diagnostics;

namespace BusTerminal.Api.Features.Discovery.Shared.Telemetry;

// Spec 009 / T021 + R-12. Single ActivitySource for the discovery slice on
// the API side. The worker has its own parallel ActivitySource (Indexer)
// with the same name so spans correlate across the queue boundary via the
// W3C traceparent on the Service Bus envelope.
//
// Span name conventions (data-model.md §6 + R-12):
//   discovery.run               — root run span (worker side)
//   discovery.fetch.{scope}     — per-entity-type fetch from ARM (worker)
//   discovery.classify          — per-batch classification (worker)
//   discovery.persist.batch     — per-batch Cosmos write (worker)
//   discovery.api.start         — API-side: start-discovery endpoint
//   discovery.api.coalesce      — API-side: coalescing decision
public static class DiscoveryActivitySource
{
    public const string Name = "BusTerminal.Discovery";
    public static readonly ActivitySource Instance = new(Name);

    public static class SpanNames
    {
        public const string ApiStartDiscovery = "discovery.api.start";
        public const string ApiCoalesce = "discovery.api.coalesce";
        public const string Run = "discovery.run";
        public const string FetchQueues = "discovery.fetch.queues";
        public const string FetchTopics = "discovery.fetch.topics";
        public const string FetchSubscriptions = "discovery.fetch.subscriptions";
        public const string FetchRules = "discovery.fetch.rules";
        public const string Classify = "discovery.classify";
        public const string PersistBatch = "discovery.persist.batch";
    }

    public static class AttributeKeys
    {
        // PII-free: only identifiers + counts + outcomes.
        public const string RunId = "discovery.run_id";
        public const string NamespaceId = "discovery.namespace_id";
        public const string EntityType = "discovery.entity_type";
        public const string FetchCount = "discovery.fetch.count";
        public const string ClassifyBatchSize = "discovery.classify.batch_size";
        public const string ClassifyDurationMs = "discovery.classify.duration_ms";
        public const string PersistBatchSize = "discovery.persist.batch_size";
        public const string PersistRuConsumed = "discovery.persist.ru_consumed";
        public const string CoalescedFromExisting = "discovery.coalesced_from_existing";
        public const string Outcome = "discovery.outcome";
        public const string FailureCategory = "discovery.failure_category";
        public const string FailurePhase = "discovery.failure_phase";
    }
}
