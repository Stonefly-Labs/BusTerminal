using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Namespaces.Validation;
using BusTerminal.Api.Features.Namespaces.Validation.Checks;
using BusTerminal.Api.Infrastructure.ServiceBus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Namespaces.Validation;

// Spec 008 / T060. Aggregate scoring + persistence + drift detection.
// Per-check OTel span emission is exercised indirectly through the
// CheckExecutor (covered alongside the individual check tests). The runner
// test focuses on the orchestrator's responsibilities.
public sealed class NamespaceValidationRunnerTests
{
    private static readonly NamespaceArmId ArmId = new(
        CanonicalArmId: "/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/ns",
        SubscriptionId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
        ResourceGroup: "rg",
        NamespaceName: "ns");

    [Fact]
    public async Task ExecuteAsync_AllPass_AggregatesHealthy_AndPersists()
    {
        var store = new InMemoryRunStore();
        var snapshot = new ArmResourceSnapshot("eastus2", "rg", ArmId.SubscriptionId, DateTimeOffset.UtcNow);
        var checks = new INamespaceValidationCheck[]
        {
            new FakeCheck(ValidationCheckName.Existence, ValidationCheckOutcome.Pass, snapshot),
            new FakeCheck(ValidationCheckName.Accessibility, ValidationCheckOutcome.Pass),
            new FakeCheck(ValidationCheckName.RequiredPermissions, ValidationCheckOutcome.Pass),
            new FakeCheck(ValidationCheckName.IdentityAuthorization, ValidationCheckOutcome.Pass),
            new FakeCheck(ValidationCheckName.ApiReachability, ValidationCheckOutcome.Pass),
        };
        var runner = NewRunner(checks, store);

        var request = NewRequest();
        var run = await runner.ExecuteAsync(request, CancellationToken.None);

        run.AggregateStatus.Should().Be(ValidationStatus.Healthy);
        run.CheckResults.Should().HaveCount(5);
        run.ArmResourceSnapshot.Should().NotBeNull();
        run.ArmResourceSnapshot!.Region.Should().Be("eastus2");
        store.Items.Should().ContainSingle().Which.Id.Should().Be(run.Id);
    }

    [Fact]
    public async Task ExecuteAsync_ExistenceFail_AggregatesUnhealthy()
    {
        var checks = new INamespaceValidationCheck[]
        {
            new FakeCheck(ValidationCheckName.Existence, ValidationCheckOutcome.Fail, snapshot: null,
                category: ValidationFailureCategory.NotFound, reason: "ArmNamespaceNotFound"),
            new FakeCheck(ValidationCheckName.Accessibility, ValidationCheckOutcome.Pass),
            new FakeCheck(ValidationCheckName.RequiredPermissions, ValidationCheckOutcome.Pass),
            new FakeCheck(ValidationCheckName.IdentityAuthorization, ValidationCheckOutcome.Pass),
            new FakeCheck(ValidationCheckName.ApiReachability, ValidationCheckOutcome.Pass),
        };
        var runner = NewRunner(checks, new InMemoryRunStore());

        var run = await runner.ExecuteAsync(NewRequest(), CancellationToken.None);

        run.AggregateStatus.Should().Be(ValidationStatus.Unhealthy);
        run.ArmResourceSnapshot.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_NonFatalFail_AggregatesDegraded()
    {
        var snapshot = new ArmResourceSnapshot("eastus2", "rg", ArmId.SubscriptionId, DateTimeOffset.UtcNow);
        var checks = new INamespaceValidationCheck[]
        {
            new FakeCheck(ValidationCheckName.Existence, ValidationCheckOutcome.Pass, snapshot),
            new FakeCheck(ValidationCheckName.Accessibility, ValidationCheckOutcome.Pass),
            new FakeCheck(ValidationCheckName.RequiredPermissions, ValidationCheckOutcome.Fail,
                category: ValidationFailureCategory.Unauthorized, reason: "ReaderRoleMissing"),
            new FakeCheck(ValidationCheckName.IdentityAuthorization, ValidationCheckOutcome.Pass),
            new FakeCheck(ValidationCheckName.ApiReachability, ValidationCheckOutcome.Pass),
        };
        var runner = NewRunner(checks, new InMemoryRunStore());

        var run = await runner.ExecuteAsync(NewRequest(), CancellationToken.None);

        run.AggregateStatus.Should().Be(ValidationStatus.Degraded);
    }

    [Fact]
    public async Task ExecuteAsync_DriftDetected_PopulatesDriftFields()
    {
        var observedSnapshot = new ArmResourceSnapshot("westus2", "rg-new", ArmId.SubscriptionId, DateTimeOffset.UtcNow);
        var baseline = new PersistedNamespaceBaseline("eastus2", "rg", ArmId.SubscriptionId);
        var checks = new INamespaceValidationCheck[]
        {
            new FakeCheck(ValidationCheckName.Existence, ValidationCheckOutcome.Pass, observedSnapshot),
            new FakeCheck(ValidationCheckName.Accessibility, ValidationCheckOutcome.Pass),
            new FakeCheck(ValidationCheckName.RequiredPermissions, ValidationCheckOutcome.Pass),
            new FakeCheck(ValidationCheckName.IdentityAuthorization, ValidationCheckOutcome.Pass),
            new FakeCheck(ValidationCheckName.ApiReachability, ValidationCheckOutcome.Pass),
        };
        var runner = NewRunner(checks, new InMemoryRunStore());

        var request = NewRequest() with { PersistedDriftBaseline = baseline };
        var run = await runner.ExecuteAsync(request, CancellationToken.None);

        run.DriftDetected.Should().BeTrue();
        run.DriftFields.Should().HaveCount(2);
        run.DriftFields.Should().Contain(f => f.Field == "region" && f.ObservedValue == "westus2");
        run.DriftFields.Should().Contain(f => f.Field == "resourceGroup");
    }

    [Fact]
    public async Task ExecuteAsync_ChecksRunInParallel()
    {
        var observed = new System.Collections.Concurrent.ConcurrentBag<DateTimeOffset>();
        async Task<CheckExecutionResult> Marker(ValidationCheckName name)
        {
            observed.Add(DateTimeOffset.UtcNow);
            await Task.Delay(100).ConfigureAwait(false);
            return new CheckExecutionResult(
                new ValidationCheckResult(name, ValidationCheckOutcome.Pass, "OK", ValidationFailureCategory.Ok, 100),
                Snapshot: null);
        }
        var checks = new INamespaceValidationCheck[]
        {
            new DelegateCheck(ValidationCheckName.Existence, () => Marker(ValidationCheckName.Existence)),
            new DelegateCheck(ValidationCheckName.Accessibility, () => Marker(ValidationCheckName.Accessibility)),
            new DelegateCheck(ValidationCheckName.RequiredPermissions, () => Marker(ValidationCheckName.RequiredPermissions)),
            new DelegateCheck(ValidationCheckName.IdentityAuthorization, () => Marker(ValidationCheckName.IdentityAuthorization)),
            new DelegateCheck(ValidationCheckName.ApiReachability, () => Marker(ValidationCheckName.ApiReachability)),
        };
        var runner = NewRunner(checks, new InMemoryRunStore());

        var start = DateTimeOffset.UtcNow;
        var run = await runner.ExecuteAsync(NewRequest(), CancellationToken.None);
        var elapsed = DateTimeOffset.UtcNow - start;

        // Five 100ms checks in parallel should finish well under 5×100ms.
        elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(400));
        run.CheckResults.Should().HaveCount(5);
    }

    private static NamespaceValidationRunRequest NewRequest()
        => new(
            RunId: Guid.NewGuid(),
            NamespaceId: Guid.NewGuid(),
            ArmId: ArmId,
            Environment: "dev",
            ExecutedBy: Guid.NewGuid(),
            ExecutedByDisplayNameSnapshot: "Test User",
            PersistedDriftBaseline: null);

    private static NamespaceValidationRunner NewRunner(
        IEnumerable<INamespaceValidationCheck> checks,
        InMemoryRunStore store)
    {
        var options = Options.Create(new ArmNamespaceProbeOptions());
        return new NamespaceValidationRunner(
            checks,
            store,
            TimeProvider.System,
            options,
            NullLogger<NamespaceValidationRunner>.Instance);
    }

    private sealed class FakeCheck : INamespaceValidationCheck
    {
        private readonly ValidationCheckOutcome _outcome;
        private readonly ArmResourceSnapshot? _snapshot;
        private readonly ValidationFailureCategory _category;
        private readonly string _reason;

        public FakeCheck(
            ValidationCheckName name,
            ValidationCheckOutcome outcome,
            ArmResourceSnapshot? snapshot = null,
            ValidationFailureCategory category = ValidationFailureCategory.Ok,
            string reason = "OK")
        {
            Name = name;
            _outcome = outcome;
            _snapshot = snapshot;
            _category = outcome == ValidationCheckOutcome.Pass
                ? ValidationFailureCategory.Ok
                : category;
            _reason = reason;
        }

        public ValidationCheckName Name { get; }

        public Task<CheckExecutionResult> ExecuteAsync(NamespaceArmId armId, CancellationToken cancellationToken)
            => Task.FromResult(new CheckExecutionResult(
                new ValidationCheckResult(Name, _outcome, _reason, _category, DurationMs: 1),
                _snapshot));
    }

    private sealed class DelegateCheck : INamespaceValidationCheck
    {
        private readonly Func<Task<CheckExecutionResult>> _factory;
        public DelegateCheck(ValidationCheckName name, Func<Task<CheckExecutionResult>> factory)
        {
            Name = name;
            _factory = factory;
        }
        public ValidationCheckName Name { get; }
        public Task<CheckExecutionResult> ExecuteAsync(NamespaceArmId armId, CancellationToken cancellationToken)
            => _factory();
    }

    private sealed class InMemoryRunStore : INamespaceValidationRunStore
    {
        public List<ValidationRun> Items { get; } = new();

        public Task AppendAsync(ValidationRun run, CancellationToken cancellationToken)
        {
            Items.Add(run);
            return Task.CompletedTask;
        }

        public Task<ValidationRunPage> ListForNamespaceAsync(
            Guid namespaceId,
            int limit,
            string? continuationToken,
            CancellationToken cancellationToken)
            => Task.FromResult(new ValidationRunPage(
                Items.Where(r => r.NamespaceId == namespaceId).Take(limit).ToArray(),
                ContinuationToken: null));

        public Task<ValidationRun?> GetAsync(Guid namespaceId, Guid runId, CancellationToken cancellationToken)
            => Task.FromResult(Items.FirstOrDefault(r => r.NamespaceId == namespaceId && r.Id == runId));
    }
}
