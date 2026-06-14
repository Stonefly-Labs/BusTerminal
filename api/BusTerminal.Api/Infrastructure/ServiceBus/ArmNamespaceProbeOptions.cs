namespace BusTerminal.Api.Infrastructure.ServiceBus;

// Spec 008 / research §5 + §14. Per-check timeout configuration for the ARM
// probe. Bound to configuration section "NamespaceValidation" (the same
// section the runner reads its aggregate budget from in Phase 3).
//
// The runner orchestrates five checks in parallel against a 15s aggregate
// budget (FR-015 / FR-039 / SC-004). Each individual check is bounded by
// PerCheckTimeout — the ARM management plane responds well under 3s p99
// under normal load; 5s gives ~2x headroom before the per-check cancel
// fires.
//
// ApiReachabilityTimeout is tighter (3s) because that check is strictly
// a "can we reach the management endpoint" probe — slower than 3s means
// network degradation, not normal latency.
public sealed class ArmNamespaceProbeOptions
{
    public const string SectionName = "NamespaceValidation";

    /// <summary>Per-check ARM call timeout. Default 5s per research §5.</summary>
    public TimeSpan PerCheckTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Per-check API reachability probe timeout. Default 3s per research §14.</summary>
    public TimeSpan ApiReachabilityTimeout { get; set; } = TimeSpan.FromSeconds(3);
}
