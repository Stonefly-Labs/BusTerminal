using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Infrastructure.Persistence;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / T093 / FR-025 + Q2. CRUD against the emulator: create → read →
// update with valid ETag (success) → update with stale ETag (ConcurrencyConflictException).
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class CanonicalStoreIntegrationTests
{
    private readonly CosmosEmulatorFixture _fixture;

    public CanonicalStoreIntegrationTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    [Fact]
    public async Task Create_then_read_returns_same_document_with_etag()
    {
        var queue = NewQueue("crud-create-q");
        var written = await _fixture.Store.CreateAsync(queue, TestActor, "integration-test", default);

        written.ConcurrencyToken.Value.Should().NotBeEmpty();

        var read = await _fixture.Store.GetAsync(queue.Id, ResourceTypeDiscriminators.Queue, includeDeleted: false, default);

        read.Should().NotBeNull();
        read!.Id.Should().Be(queue.Id);
        read.ConcurrencyToken.Value.Should().Be(written.ConcurrencyToken.Value);
    }

    [Fact]
    public async Task Update_with_valid_etag_succeeds_and_rotates_token()
    {
        var queue = NewQueue("crud-update-valid-q");
        var written = await _fixture.Store.CreateAsync(queue, TestActor, "integration-test", default);

        var mutated = (Queue)written with { DisplayName = "Updated display name" };
        var updated = await _fixture.Store.UpdateAsync(mutated, TestActor, "integration-test", default);

        updated.DisplayName.Should().Be("Updated display name");
        updated.ConcurrencyToken.Value.Should().NotBe(written.ConcurrencyToken.Value, "Cosmos rotates _etag on every write");
    }

    [Fact]
    public async Task Update_with_stale_etag_throws_ConcurrencyConflictException()
    {
        var queue = NewQueue("crud-update-stale-q");
        var written = await _fixture.Store.CreateAsync(queue, TestActor, "integration-test", default);

        // Sequence two writes; the second carries the original (now stale) token.
        var firstUpdate = (Queue)written with { DisplayName = "First update" };
        await _fixture.Store.UpdateAsync(firstUpdate, TestActor, "integration-test", default);

        var staleAttempt = (Queue)written with { DisplayName = "Second update — stale" };

        var act = async () => await _fixture.Store.UpdateAsync(staleAttempt, TestActor, "integration-test", default);

        var ex = await act.Should().ThrowAsync<ConcurrencyConflictException>();
        ex.Which.ResourceId.Should().Be(queue.Id);
        ex.Which.PresentedToken.Value.Should().Be(written.ConcurrencyToken.Value);
    }

    private static Queue NewQueue(string name) => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Queue,
        Name = new ResourceName(name),
        DisplayName = $"CRUD {name}",
        NamespacePath = new NamespacePath("enterprise/test"),
        Lifecycle = LifecycleState.Active,
        Version = new SemanticVersion(1, 0, 0),
        Audit = new AuditRecord(
            CreatedBy: TestActor,
            CreatedAt: DateTimeOffset.UtcNow,
            ModifiedBy: TestActor,
            ModifiedAt: DateTimeOffset.UtcNow),
        Ownership = new OwnershipRecord(
            OwningTeamId: ResourceId.New(),
            OperationalTier: OperationalTier.Tier1),
        QueueKind = "AzureServiceBus",
        Ordering = OrderingPolicy.Fifo,
    };
}
