using BusTerminal.Api.Features.Discovery.Shared;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Infrastructure.Persistence;
using BusTerminal.Api.Tests.Features.Registry._Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.ArchiveEntity;

// Spec 009 / T097 + FR-015. Sticky-archive: once an entity is Archived,
// subsequent discovery upserts MUST NOT auto-revert the lifecycle to
// Active. UpsertAzureSourcedAsync intentionally omits /lifecycleStatus
// from its PATCH operations, so this is a direct test of that invariant.
[Collection("RegistryFixture")]
[Trait("Category", "Integration")]
public sealed class ArchiveStickyIntegrationTests
{
    private readonly RegistryFixture _fixture;

    public ArchiveStickyIntegrationTests(RegistryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Archived_StaysArchived_AcrossDiscoveryRediscovery()
    {
        if (!_fixture.ShouldRun()) return;

        var store = CreateStore();
        var env = _fixture.Environment;
        var ns = $"ns_test_{Guid.NewGuid():N}";
        var entityId = PublishedEntityIdComputer.ComputeFor(EntityType.Queue, ns, leafName: "orders");

        var initial = NewUpsert(entityId, env, ns, "orders");
        await store.UpsertAzureSourcedAsync(initial, ifMatch: null, CancellationToken.None);

        var afterCreate = await store.GetDetailAsync(entityId, env, CancellationToken.None);
        afterCreate.Should().NotBeNull();
        afterCreate!.Entity.LifecycleStatus.Should().Be(LifecycleStatus.Active);

        // Archive.
        var archived = await store.SetLifecycleStatusAsync(
            entityId, env, LifecycleStatus.Archived, afterCreate.ETag,
            modifiedBy: "operator-test", CancellationToken.None);
        archived.Entity.LifecycleStatus.Should().Be(LifecycleStatus.Archived);

        // Re-run discovery.
        var rediscover = initial with
        {
            AzureSourcedHash = "sha256:rediscovered",
            DiscoveryRunId = $"dr_{Guid.NewGuid():N}"[..18],
            DiscoveryRunStartedUtc = DateTimeOffset.UtcNow.AddSeconds(60),
        };
        await store.UpsertAzureSourcedAsync(rediscover, ifMatch: null, CancellationToken.None);

        // Verify lifecycle stayed Archived.
        var afterRediscovery = await store.GetDetailAsync(entityId, env, CancellationToken.None);
        afterRediscovery.Should().NotBeNull();
        afterRediscovery!.Entity.LifecycleStatus.Should().Be(LifecycleStatus.Archived);
        // azureSourced still gets updated.
        afterRediscovery.Entity.AzureSourcedHash.Should().Be("sha256:rediscovered");
    }

    private CosmosPublishedEntityStore CreateStore()
    {
        var options = Options.Create(new CosmosRegistryOptions
        {
            Database = "canonical",
            EntitiesContainer = System.Environment
                .GetEnvironmentVariable("BUSTERMINAL_TEST_REGISTRY_CONTAINER") ?? "registry-entities",
            AuditContainer = "registry-audit",
            LeasesContainer = "registry-entities-leases",
            ValidationRunsContainer = "namespace-validation-runs",
            DiscoveryRunsContainer = "discovery-runs",
            DiscoveryLocksContainer = "discovery-locks",
        });
        return new CosmosPublishedEntityStore(_fixture.Client, options, NullLogger<CosmosPublishedEntityStore>.Instance);
    }

    private static DiscoveredEntityUpsert NewUpsert(string entityId, string env, string namespaceId, string name)
    {
        var sourced = new AzureSourcedQueue(
            $"/subscriptions/.../namespaces/test/queues/{name}", null, "Active",
            "PT1M", 10,
            new AzureSourcedDuplicateDetection(false, null),
            new AzureSourcedDeadLettering(true),
            new AzureSourcedPartitioning(false),
            new AzureSourcedSession(false),
            new AzureSourcedForwarding(null, null),
            "P14D", 5120);
        return new DiscoveredEntityUpsert(
            EntityId: entityId,
            Environment: env,
            EntityType: EntityType.Queue,
            NamespaceId: namespaceId,
            Name: name,
            DisplayName: name,
            CompositeKey: PublishedEntityIdComputer.ComposeCompositeKey(EntityType.Queue, namespaceId, leafName: name),
            ParentEntityId: null,
            AzureSourced: sourced,
            AzureSourcedHash: "sha256:initial",
            DiscoveryRunId: $"dr_{Guid.NewGuid():N}"[..18],
            DiscoveryRunStartedUtc: DateTimeOffset.UtcNow,
            DiscoveredBy: "user-test");
    }
}
