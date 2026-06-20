using System.Diagnostics;

namespace BusTerminal.Indexer.Discovery.Telemetry;

// Spec 009 / T022 + R-12. Worker-side ActivitySource matching the API-side
// source name. The Functions Service Bus binding seeds each invocation's
// Activity from the message's `Diagnostic-Id` property, which carries the
// originating API request's W3C traceparent (set on send by the API's
// DiscoveryRequestPublisher). Worker spans become children of the API span
// in App Insights.
//
// Constant duplication with the API project is intentional — the worker is
// a separate compilation unit and importing the API assembly would pull a
// large dependency graph the Functions host doesn't need.
public static class DiscoveryActivitySource
{
    public const string Name = "BusTerminal.Discovery";
    public static readonly ActivitySource Instance = new(Name);

    public static class SpanNames
    {
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
        public const string RunId = "discovery.run_id";
        public const string NamespaceId = "discovery.namespace_id";
        public const string EntityType = "discovery.entity_type";
        public const string FetchCount = "discovery.fetch.count";
        public const string ClassifyBatchSize = "discovery.classify.batch_size";
        public const string ClassifyDurationMs = "discovery.classify.duration_ms";
        public const string PersistBatchSize = "discovery.persist.batch_size";
        public const string PersistRuConsumed = "discovery.persist.ru_consumed";
        public const string Outcome = "discovery.outcome";
        public const string FailureCategory = "discovery.failure_category";
        public const string FailurePhase = "discovery.failure_phase";
    }
}
