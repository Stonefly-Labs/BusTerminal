using System.Diagnostics.Metrics;

namespace BusTerminal.Indexer.Indexing.Telemetry;

// Issue #118 — domain metrics for the change-feed → AI Search indexing hot
// path (RegistryEntityIndexer). These are deliberately the signals the
// Functions host CANNOT derive on its own: the host already emits per-
// invocation request telemetry (duration, success/failure, faas.trigger) once
// telemetryMode=OpenTelemetry is on, so we do NOT re-count invocations here —
// that would duplicate host telemetry. Instead we measure business throughput,
// batching shape, AI Search call latency, and a typed failure breakdown.
//
// Mirrors the DiscoveryMeter pattern (IMeterFactory-created, const instrument/
// tag names) so dashboards and tests treat both meters the same way. Subscribed
// in Program.cs via AddMeter(IndexerMeter.Name).
public sealed class IndexerMeter
{
    public const string Name = "BusTerminal.Indexer";

    public const string InstrumentDocumentsIndexed = "indexer.documents.indexed";
    public const string InstrumentBatchSize = "indexer.batch.size";
    public const string InstrumentFailures = "indexer.failures";
    public const string InstrumentAiSearchDuration = "indexer.aisearch.duration";

    public const string TagOperation = "operation";
    public const string TagCategory = "category";

    public const string OperationUpsert = "upsert";
    public const string OperationDelete = "delete";

    // Count of documents written to AI Search, tagged operation=upsert|delete.
    // Business volume — not derivable from the host's invocation count.
    public Counter<long> DocumentsIndexed { get; }

    // Distribution of change-feed items per invocation. Surfaces batching
    // efficiency and the shape of change-feed throughput.
    public Histogram<int> BatchSize { get; }

    // Permanent-failure count tagged with the RegistryEntityIndexer's
    // ClassifyError bucket (unauthorized | aiSearchSchema | mapping | transient)
    // for an instant, actionable failure breakdown.
    public Counter<long> Failures { get; }

    // Latency (ms) of the AI Search MergeOrUpload / Delete calls, tagged
    // operation=upsert|delete. Backs the spec's "propagates within 5s p95"
    // SLO with a percentile-alertable histogram.
    public Histogram<double> AiSearchDuration { get; }

    public IndexerMeter(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(Name);
        DocumentsIndexed = meter.CreateCounter<long>(InstrumentDocumentsIndexed);
        BatchSize = meter.CreateHistogram<int>(InstrumentBatchSize, unit: "{item}");
        Failures = meter.CreateCounter<long>(InstrumentFailures);
        AiSearchDuration = meter.CreateHistogram<double>(InstrumentAiSearchDuration, unit: "ms");
    }
}
