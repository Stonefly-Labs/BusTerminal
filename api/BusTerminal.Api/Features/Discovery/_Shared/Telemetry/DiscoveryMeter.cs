using System.Diagnostics.Metrics;

namespace BusTerminal.Api.Features.Discovery.Shared.Telemetry;

// Spec 009 / T021 + R-12. Discovery meter and the five instruments documented
// in the research record. Dimension cardinality is deliberately bounded —
// every dimension value comes from a closed enum. Resolved via IMeterFactory
// (Generic Host default) so the Meter lifetime matches the application's.
public sealed class DiscoveryMeter
{
    public const string Name = "BusTerminal.Discovery";

    public const string InstrumentRunsStarted = "discovery.runs.started";
    public const string InstrumentRunsCompleted = "discovery.runs.completed";
    public const string InstrumentRunDuration = "discovery.run.duration";
    public const string InstrumentEntitiesClassified = "discovery.entities.classified";
    public const string InstrumentArmRetries = "discovery.arm.retries";

    public const string TagOutcome = "outcome";          // new|coalesced
    public const string TagStatus = "status";            // succeeded|failed
    public const string TagFailureCategory = "failure_category";
    public const string TagNamespaceTier = "namespace_tier"; // small|medium|large
    public const string TagEntityType = "entity_type";
    public const string TagClassificationOutcome = "classification_outcome";
    public const string TagFailureClass = "failure_class";

    public Counter<long> RunsStarted { get; }
    public Counter<long> RunsCompleted { get; }
    public Histogram<double> RunDuration { get; }
    public Counter<long> EntitiesClassified { get; }
    public Counter<long> ArmRetries { get; }

    public DiscoveryMeter(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(Name);
        RunsStarted = meter.CreateCounter<long>(InstrumentRunsStarted);
        RunsCompleted = meter.CreateCounter<long>(InstrumentRunsCompleted);
        RunDuration = meter.CreateHistogram<double>(InstrumentRunDuration, unit: "ms");
        EntitiesClassified = meter.CreateCounter<long>(InstrumentEntitiesClassified);
        ArmRetries = meter.CreateCounter<long>(InstrumentArmRetries);
    }
}
