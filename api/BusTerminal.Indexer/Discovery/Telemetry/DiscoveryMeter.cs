using System.Diagnostics.Metrics;

namespace BusTerminal.Indexer.Discovery.Telemetry;

// Spec 009 / T022 + R-12. Worker-side meter. Mirror of the API-side meter
// names so dashboards aggregate cleanly across both emitters.
public sealed class DiscoveryMeter
{
    public const string Name = "BusTerminal.Discovery";

    public const string InstrumentRunsCompleted = "discovery.runs.completed";
    public const string InstrumentRunDuration = "discovery.run.duration";
    public const string InstrumentEntitiesClassified = "discovery.entities.classified";
    public const string InstrumentArmRetries = "discovery.arm.retries";

    public const string TagStatus = "status";
    public const string TagFailureCategory = "failure_category";
    public const string TagNamespaceTier = "namespace_tier";
    public const string TagEntityType = "entity_type";
    public const string TagClassificationOutcome = "classification_outcome";
    public const string TagFailureClass = "failure_class";

    public Counter<long> RunsCompleted { get; }
    public Histogram<double> RunDuration { get; }
    public Counter<long> EntitiesClassified { get; }
    public Counter<long> ArmRetries { get; }

    public DiscoveryMeter(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(Name);
        RunsCompleted = meter.CreateCounter<long>(InstrumentRunsCompleted);
        RunDuration = meter.CreateHistogram<double>(InstrumentRunDuration, unit: "ms");
        EntitiesClassified = meter.CreateCounter<long>(InstrumentEntitiesClassified);
        ArmRetries = meter.CreateCounter<long>(InstrumentArmRetries);
    }
}
