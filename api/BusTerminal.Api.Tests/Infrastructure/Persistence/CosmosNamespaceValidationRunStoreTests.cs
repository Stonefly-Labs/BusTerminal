using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Infrastructure.Persistence;
using BusTerminal.Api.Tests.Features.Registry._Shared;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Tests.Infrastructure.Persistence;

// Spec 008 / T032. Append + namespace-scoped read coverage. The store has no
// update/delete surface — runs are immutable per FR-016 — so this test
// matrix is: append, list time-descending, get-by-id, duplicate-id rejection.
[Collection("RegistryFixture")]
[Trait("Category", "Integration")]
public class CosmosNamespaceValidationRunStoreTests
{
    private readonly RegistryFixture _fixture;

    public CosmosNamespaceValidationRunStoreTests(RegistryFixture fixture)
    {
        _fixture = fixture;
    }

    private CosmosNamespaceValidationRunStore CreateStore()
    {
        var options = Options.Create(new CosmosRegistryOptions
        {
            Database = "canonical",
            EntitiesContainer = "registry-entities",
            AuditContainer = "registry-audit",
            LeasesContainer = "registry-entities-leases",
            ValidationRunsContainer = System.Environment
                .GetEnvironmentVariable("BUSTERMINAL_TEST_VALIDATION_RUNS_CONTAINER")
                ?? "namespace-validation-runs",
        });
        return new CosmosNamespaceValidationRunStore(
            _fixture.Client,
            options,
            NullLogger<CosmosNamespaceValidationRunStore>.Instance);
    }

    [Fact]
    public async Task Append_then_list_returns_run_in_time_descending_order()
    {
        if (!_fixture.ShouldRun()) return;

        var store = CreateStore();
        var namespaceId = Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow.AddSeconds(-10);
        var older = NewRun(namespaceId, t0);
        var newer = NewRun(namespaceId, t0.AddSeconds(5));

        await store.AppendAsync(older, CancellationToken.None);
        await store.AppendAsync(newer, CancellationToken.None);

        var page = await store.ListForNamespaceAsync(namespaceId, limit: 10, continuationToken: null, CancellationToken.None);

        page.Items.Should().HaveCountGreaterThanOrEqualTo(2);
        page.Items[0].Id.Should().Be(newer.Id, "list is ordered by executedAtUtc DESC");
        page.Items[1].Id.Should().Be(older.Id);
    }

    [Fact]
    public async Task Get_returns_persisted_run_by_id()
    {
        if (!_fixture.ShouldRun()) return;

        var store = CreateStore();
        var namespaceId = Guid.NewGuid();
        var run = NewRun(namespaceId, DateTimeOffset.UtcNow);
        await store.AppendAsync(run, CancellationToken.None);

        var fetched = await store.GetAsync(namespaceId, run.Id, CancellationToken.None);

        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(run.Id);
        fetched.NamespaceId.Should().Be(namespaceId);
        fetched.AggregateStatus.Should().Be(run.AggregateStatus);
        fetched.CheckResults.Should().HaveCount(5);
    }

    [Fact]
    public async Task Get_returns_null_when_run_id_unknown()
    {
        if (!_fixture.ShouldRun()) return;

        var store = CreateStore();
        var namespaceId = Guid.NewGuid();
        var notFound = await store.GetAsync(namespaceId, Guid.NewGuid(), CancellationToken.None);

        notFound.Should().BeNull();
    }

    [Fact]
    public async Task Duplicate_run_id_in_same_partition_throws_invalid_operation()
    {
        if (!_fixture.ShouldRun()) return;

        var store = CreateStore();
        var namespaceId = Guid.NewGuid();
        var run = NewRun(namespaceId, DateTimeOffset.UtcNow);
        await store.AppendAsync(run, CancellationToken.None);

        var act = () => store.AppendAsync(run, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static ValidationRun NewRun(Guid namespaceId, DateTimeOffset executedAtUtc)
    {
        var checks = new[]
        {
            new ValidationCheckResult(
                ValidationCheckName.Existence,
                ValidationCheckOutcome.Pass,
                "OK",
                ValidationFailureCategory.Ok,
                DurationMs: 12),
            new ValidationCheckResult(
                ValidationCheckName.Accessibility,
                ValidationCheckOutcome.Pass,
                "OK",
                ValidationFailureCategory.Ok,
                DurationMs: 13),
            new ValidationCheckResult(
                ValidationCheckName.RequiredPermissions,
                ValidationCheckOutcome.Pass,
                "OK",
                ValidationFailureCategory.Ok,
                DurationMs: 14),
            new ValidationCheckResult(
                ValidationCheckName.IdentityAuthorization,
                ValidationCheckOutcome.Pass,
                "OK",
                ValidationFailureCategory.Ok,
                DurationMs: 15),
            new ValidationCheckResult(
                ValidationCheckName.ApiReachability,
                ValidationCheckOutcome.Pass,
                "OK",
                ValidationFailureCategory.Ok,
                DurationMs: 16),
        };
        return new ValidationRun(
            Id: Guid.NewGuid(),
            NamespaceId: namespaceId,
            ExecutedAtUtc: executedAtUtc,
            ExecutedBy: Guid.NewGuid(),
            ExecutedByDisplayNameSnapshot: "Test Operator",
            AzureResourceIdAtRun: $"/subscriptions/{Guid.NewGuid():D}/resourceGroups/rg-test/providers/Microsoft.ServiceBus/namespaces/ns-test-eus2",
            AggregateStatus: ValidationStatus.Healthy,
            CheckResults: checks,
            DriftDetected: false,
            DriftFields: Array.Empty<DriftField>(),
            TotalDurationMs: 70);
    }
}
