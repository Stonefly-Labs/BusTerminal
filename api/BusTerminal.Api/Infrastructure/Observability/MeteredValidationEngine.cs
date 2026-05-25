using System.Diagnostics.Metrics;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Relationships;
using BusTerminal.Api.Domain.Validation;

namespace BusTerminal.Api.Infrastructure.Observability;

// Spec 004 / T153 / data-model.md § Naming Cross-Reference.
//
// Wraps an inner IValidationEngine and emits one Counter increment per
// finding-severity-and-resource-type bucket on every validation pass. The
// three counter names — `busterminal.validation.finding_count_error`,
// `..._warning`, `..._info` — are mandated by the naming cross-reference and
// must not drift.
//
// The decorator pattern keeps OTel metric concerns out of the engine itself:
// ValidationEngine stays a pure rule dispatcher (and remains directly
// constructible by unit tests), while production wiring resolves
// IValidationEngine to this decorator so every code path is instrumented.
//
// IMeterFactory is obtained from DI (registered automatically by Generic Host
// in .NET 8+); the Meter's lifetime is managed by the factory.
public sealed class MeteredValidationEngine : IValidationEngine
{
    private readonly IValidationEngine _inner;
    private readonly Counter<long> _errorCount;
    private readonly Counter<long> _warningCount;
    private readonly Counter<long> _infoCount;

    public MeteredValidationEngine(IValidationEngine inner, IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(meterFactory);

        _inner = inner;

        var meter = meterFactory.Create(ValidationMeter.Name);
        _errorCount = meter.CreateCounter<long>(ValidationMeter.FindingCountError);
        _warningCount = meter.CreateCounter<long>(ValidationMeter.FindingCountWarning);
        _infoCount = meter.CreateCounter<long>(ValidationMeter.FindingCountInfo);
    }

    public async Task<ValidationResult> ValidateAsync(
        Resource resource,
        Func<ResourceId, Resource?> relationshipResolver,
        Func<Resource, bool> duplicateDetector,
        LifecycleState? previousLifecycle = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var result = await _inner.ValidateAsync(
            resource,
            relationshipResolver,
            duplicateDetector,
            previousLifecycle,
            cancellationToken).ConfigureAwait(false);

        EmitFindingCounts(result, resource.ResourceType);

        return result;
    }

    public async Task<ValidationResult> ValidateRelationshipAsync(
        Relationship relationship,
        Func<ResourceId, Resource?> relationshipResolver,
        Func<Resource, bool> duplicateDetector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        var result = await _inner.ValidateRelationshipAsync(
            relationship,
            relationshipResolver,
            duplicateDetector,
            cancellationToken).ConfigureAwait(false);

        // Relationship documents share the validation counter pool — discriminator
        // value is the literal "relationship" so dashboards can filter cleanly.
        EmitFindingCounts(result, ResourceTypeDiscriminators.Relationship);

        return result;
    }

    private void EmitFindingCounts(ValidationResult result, string resourceTypeDiscriminator)
    {
        var error = 0L;
        var warning = 0L;
        var info = 0L;

        foreach (var finding in result.Findings)
        {
            switch (finding.Severity)
            {
                case ValidationSeverity.Error:
                    error++;
                    break;
                case ValidationSeverity.Warning:
                    warning++;
                    break;
                case ValidationSeverity.Info:
                    info++;
                    break;
            }
        }

        var tag = new KeyValuePair<string, object?>(ValidationMeter.ResourceTypeTag, resourceTypeDiscriminator);

        // Always emit each counter — recording 0 keeps the per-resource-type
        // series alive in the histogram, which makes "rate of error findings"
        // dashboards reliable instead of jumpy.
        _errorCount.Add(error, tag);
        _warningCount.Add(warning, tag);
        _infoCount.Add(info, tag);
    }
}
