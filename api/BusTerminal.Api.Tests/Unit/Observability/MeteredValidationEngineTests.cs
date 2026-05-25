using System.Diagnostics.Metrics;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Lifecycle;
using BusTerminal.Api.Domain.Relationships;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Api.Tests.Unit.Observability;

// Spec 004 / T153. Verifies the three OTel Counter instruments on
// MeteredValidationEngine fire with the correct names, severity-bucketed
// values, and resource-type tag on every validation pass. Without this guard,
// a future refactor could silently break the metrics pipeline — the names
// (`busterminal.validation.finding_count_{error,warning,info}`) are mandated
// by data-model.md § Naming Cross-Reference and must not drift.
public sealed class MeteredValidationEngineTests
{
    [Fact]
    public async Task ValidateAsync_emits_one_increment_per_severity_bucket_with_resource_type_tag()
    {
        var stub = new StubValidationEngine(
        [
            Finding("rule.a", ValidationSeverity.Error),
            Finding("rule.b", ValidationSeverity.Warning),
            Finding("rule.c", ValidationSeverity.Warning),
            Finding("rule.d", ValidationSeverity.Info),
            Finding("rule.e", ValidationSeverity.Info),
            Finding("rule.f", ValidationSeverity.Info),
        ]);

        await using var harness = MeterHarness.Create();
        var sut = new MeteredValidationEngine(stub, harness.Factory);

        var resource = BuildQueue();
        var result = await sut.ValidateAsync(
            resource,
            relationshipResolver: _ => null,
            duplicateDetector: _ => false);

        result.Findings.Should().HaveCount(6, "decorator must pass findings through unchanged");

        harness.Total(ValidationMeter.FindingCountError).Should().Be(1);
        harness.Total(ValidationMeter.FindingCountWarning).Should().Be(2);
        harness.Total(ValidationMeter.FindingCountInfo).Should().Be(3);

        harness.Tag(ValidationMeter.FindingCountError, ValidationMeter.ResourceTypeTag)
            .Should().Be(ResourceTypeDiscriminators.Queue);
        harness.Tag(ValidationMeter.FindingCountWarning, ValidationMeter.ResourceTypeTag)
            .Should().Be(ResourceTypeDiscriminators.Queue);
        harness.Tag(ValidationMeter.FindingCountInfo, ValidationMeter.ResourceTypeTag)
            .Should().Be(ResourceTypeDiscriminators.Queue);
    }

    [Fact]
    public async Task ValidateAsync_emits_zero_increments_when_no_findings()
    {
        var stub = new StubValidationEngine([]);

        await using var harness = MeterHarness.Create();
        var sut = new MeteredValidationEngine(stub, harness.Factory);

        await sut.ValidateAsync(
            BuildQueue(),
            relationshipResolver: _ => null,
            duplicateDetector: _ => false);

        // Each counter is always Added (with 0 when no findings) so per-resource-
        // type series stay alive in the histogram. Sample count is 1; total is 0.
        harness.Samples(ValidationMeter.FindingCountError).Should().Be(1);
        harness.Samples(ValidationMeter.FindingCountWarning).Should().Be(1);
        harness.Samples(ValidationMeter.FindingCountInfo).Should().Be(1);
        harness.Total(ValidationMeter.FindingCountError).Should().Be(0);
        harness.Total(ValidationMeter.FindingCountWarning).Should().Be(0);
        harness.Total(ValidationMeter.FindingCountInfo).Should().Be(0);
    }

    [Fact]
    public async Task ValidateRelationshipAsync_tags_increments_with_relationship_discriminator()
    {
        var stub = new StubValidationEngine(
        [
            Finding("rel.rule", ValidationSeverity.Warning),
        ]);

        await using var harness = MeterHarness.Create();
        var sut = new MeteredValidationEngine(stub, harness.Factory);

        var rel = BuildRelationship();
        await sut.ValidateRelationshipAsync(
            rel,
            relationshipResolver: _ => null,
            duplicateDetector: _ => false);

        harness.Total(ValidationMeter.FindingCountWarning).Should().Be(1);
        harness.Tag(ValidationMeter.FindingCountWarning, ValidationMeter.ResourceTypeTag)
            .Should().Be(ResourceTypeDiscriminators.Relationship);
    }

    private static ValidationFinding Finding(string ruleId, ValidationSeverity severity) =>
        new(RuleId: ruleId, Severity: severity, Message: "test", EvaluatedAt: DateTimeOffset.UtcNow);

    private static Queue BuildQueue() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Queue,
        Name = new ResourceName("metered-q"),
        DisplayName = "Metered queue",
        NamespacePath = new NamespacePath("enterprise/test"),
        Lifecycle = LifecycleState.Active,
        Version = new SemanticVersion(1, 0, 0),
        Audit = new AuditRecord(
            CreatedBy: new SystemPrincipalReference("test"),
            CreatedAt: DateTimeOffset.UtcNow,
            ModifiedBy: new SystemPrincipalReference("test"),
            ModifiedAt: DateTimeOffset.UtcNow),
        QueueKind = "AzureServiceBus",
        Ordering = OrderingPolicy.Fifo,
    };

    private static Relationship BuildRelationship() => new()
    {
        Id = ResourceId.New(),
        SourceId = ResourceId.New(),
        TargetId = ResourceId.New(),
        Type = RelationshipType.PublishesTo,
        Audit = new AuditRecord(
            CreatedBy: new SystemPrincipalReference("test"),
            CreatedAt: DateTimeOffset.UtcNow,
            ModifiedBy: new SystemPrincipalReference("test"),
            ModifiedAt: DateTimeOffset.UtcNow),
    };

    private sealed class StubValidationEngine : IValidationEngine
    {
        private readonly IReadOnlyList<ValidationFinding> _findings;

        public StubValidationEngine(IReadOnlyList<ValidationFinding> findings)
        {
            _findings = findings;
        }

        public Task<ValidationResult> ValidateAsync(
            Resource resource,
            Func<ResourceId, Resource?> relationshipResolver,
            Func<Resource, bool> duplicateDetector,
            LifecycleState? previousLifecycle = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ValidationResult(DateTimeOffset.UtcNow, _findings));

        public Task<ValidationResult> ValidateRelationshipAsync(
            Relationship relationship,
            Func<ResourceId, Resource?> relationshipResolver,
            Func<Resource, bool> duplicateDetector,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ValidationResult(DateTimeOffset.UtcNow, _findings));
    }

    // Single-meter harness: spins up an isolated IMeterFactory via Microsoft.Extensions
    // .DependencyInjection's AddMetrics, attaches a MeterListener filtered to the
    // ValidationMeter, and exposes total + tag accessors. Disposed via the
    // IAsyncDisposable so the listener stops cleanly between tests (no cross-test
    // measurement leakage).
    private sealed class MeterHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly MeterListener _listener;
        private readonly Dictionary<string, long> _totals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _samples = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, object?>> _tags = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        private MeterHarness(ServiceProvider services, MeterListener listener)
        {
            _services = services;
            _listener = listener;
        }

        public IMeterFactory Factory => _services.GetRequiredService<IMeterFactory>();

        public long Total(string instrumentName)
        {
            lock (_gate)
            {
                return _totals.TryGetValue(instrumentName, out var v) ? v : 0;
            }
        }

        public int Samples(string instrumentName)
        {
            lock (_gate)
            {
                return _samples.TryGetValue(instrumentName, out var v) ? v : 0;
            }
        }

        public object? Tag(string instrumentName, string tagKey)
        {
            lock (_gate)
            {
                return _tags.TryGetValue(instrumentName, out var bag) && bag.TryGetValue(tagKey, out var v)
                    ? v
                    : null;
            }
        }

        public static MeterHarness Create()
        {
            var services = new ServiceCollection()
                .AddMetrics()
                .BuildServiceProvider();

            var harness = new MeterHarness(services, new MeterListener());
            harness._listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ValidationMeter.Name)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            harness._listener.SetMeasurementEventCallback<long>(harness.OnMeasurement);
            harness._listener.Start();
            return harness;
        }

        private void OnMeasurement(
            Instrument instrument,
            long measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
        {
            lock (_gate)
            {
                _totals.TryGetValue(instrument.Name, out var current);
                _totals[instrument.Name] = current + measurement;

                _samples.TryGetValue(instrument.Name, out var sampleCount);
                _samples[instrument.Name] = sampleCount + 1;

                if (!_tags.TryGetValue(instrument.Name, out var bag))
                {
                    bag = new Dictionary<string, object?>(StringComparer.Ordinal);
                    _tags[instrument.Name] = bag;
                }
                foreach (var t in tags)
                {
                    bag[t.Key] = t.Value;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Dispose();
            await _services.DisposeAsync();
        }
    }
}
