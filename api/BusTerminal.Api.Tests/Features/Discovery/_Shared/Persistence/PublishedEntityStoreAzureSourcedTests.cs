using BusTerminal.Api.Features.Discovery.Shared;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Infrastructure.Persistence;
using BusTerminal.Api.Tests.Features.Registry._Shared;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery._Shared.Persistence;

// Spec 009 / T020. FR-016 invariant — UpsertAzureSourcedAsync MUST NOT
// overwrite curated metadata or serviceAssociations. The store achieves this
// via Cosmos PATCH targeting only the discovery-owned paths.
[Collection("RegistryFixture")]
[Trait("Category", "Integration")]
public sealed class PublishedEntityStoreAzureSourcedTests
{
    private readonly RegistryFixture _fixture;

    public PublishedEntityStoreAzureSourcedTests(RegistryFixture fixture)
    {
        _fixture = fixture;
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
        return new CosmosPublishedEntityStore(
            _fixture.Client, options, NullLogger<CosmosPublishedEntityStore>.Instance);
    }

    [Fact]
    public async Task UpsertAzureSourced_OnFirstSighting_CreatesDocument()
    {
        if (!_fixture.ShouldRun()) return;

        var store = CreateStore();
        var env = _fixture.Environment;
        var ns = $"ns_test_{Guid.NewGuid():N}";
        var entityId = PublishedEntityIdComputer.ComputeFor(EntityType.Queue, ns, leafName: "orders");

        var upsert = NewUpsert(entityId, env, ns, "orders", EntityType.Queue);
        await store.UpsertAzureSourcedAsync(upsert, ifMatch: null, CancellationToken.None);

        var read = await store.GetForDiscoveryAsync(entityId, env, CancellationToken.None);
        read.Should().NotBeNull();
        read!.LifecycleStatus.Should().Be(LifecycleStatus.Active);
        read.AzureSourcedHash.Should().Be(upsert.AzureSourcedHash);
    }

    [Fact]
    public async Task UpsertAzureSourced_OnRediscovery_PreservesCuratedFields()
    {
        if (!_fixture.ShouldRun()) return;

        var store = CreateStore();
        var env = _fixture.Environment;
        var ns = $"ns_test_{Guid.NewGuid():N}";
        var entityId = PublishedEntityIdComputer.ComputeFor(EntityType.Queue, ns, leafName: "orders");
        var partitionKey = new PartitionKey(env);

        // Seed: first upsert creates the document with empty curated metadata.
        var initialUpsert = NewUpsert(entityId, env, ns, "orders", EntityType.Queue);
        await store.UpsertAzureSourcedAsync(initialUpsert, ifMatch: null, CancellationToken.None);

        // Operator manually patches in curated fields (simulating a US4 edit).
        var container = _fixture.Client.GetContainer("canonical",
            System.Environment.GetEnvironmentVariable("BUSTERMINAL_TEST_REGISTRY_CONTAINER") ?? "registry-entities");
        await container.PatchItemAsync<dynamic>(entityId, partitionKey, new[]
        {
            PatchOperation.Set("/description", "Operator-curated description"),
            PatchOperation.Set("/businessPurpose", "Bridges checkout to fulfillment."),
            PatchOperation.Set("/tags", new[] { "domain:orders" }),
            PatchOperation.Set("/serviceAssociations", new[]
            {
                new
                {
                    associationId = "esa_test",
                    serviceId = "svc_payments",
                    role = "Owner",
                    createdUtc = DateTimeOffset.UtcNow,
                    createdBy = "user-test",
                },
            }),
        });

        // Re-run discovery — must NOT clobber curated fields.
        var rediscoverUpsert = initialUpsert with
        {
            AzureSourcedHash = "sha256:rediscovered",
            DiscoveryRunId = $"dr_{Guid.NewGuid():N}"[..18],
            DiscoveryRunStartedUtc = DateTimeOffset.UtcNow.AddSeconds(60),
        };
        await store.UpsertAzureSourcedAsync(rediscoverUpsert, ifMatch: null, CancellationToken.None);

        var afterRediscovery = await container.ReadItemAsync<dynamic>(entityId, partitionKey);
        var raw = afterRediscovery.Resource.ToString() as string ?? string.Empty;

        raw.Should().Contain("Operator-curated description", "curated description survives discovery");
        raw.Should().Contain("Bridges checkout to fulfillment.", "businessPurpose survives discovery");
        raw.Should().Contain("svc_payments", "service associations survive discovery");
        raw.Should().Contain("sha256:rediscovered", "azureSourcedHash IS updated");
    }

    private static DiscoveredEntityUpsert NewUpsert(
        string entityId, string env, string namespaceId, string name, EntityType entityType)
    {
        AzureSourcedEntity sourced = entityType switch
        {
            EntityType.Queue => new AzureSourcedQueue(
                $"/subscriptions/.../namespaces/test/queues/{name}", null, "Active",
                "PT1M", 10,
                new AzureSourcedDuplicateDetection(false, null),
                new AzureSourcedDeadLettering(true),
                new AzureSourcedPartitioning(false),
                new AzureSourcedSession(false),
                new AzureSourcedForwarding(null, null),
                "P14D", 5120),
            _ => throw new ArgumentOutOfRangeException(nameof(entityType)),
        };

        return new DiscoveredEntityUpsert(
            EntityId: entityId,
            Environment: env,
            EntityType: entityType,
            NamespaceId: namespaceId,
            Name: name,
            DisplayName: name,
            CompositeKey: PublishedEntityIdComputer.ComposeCompositeKey(entityType, namespaceId, leafName: name),
            ParentEntityId: null,
            AzureSourced: sourced,
            AzureSourcedHash: "sha256:initial",
            DiscoveryRunId: $"dr_{Guid.NewGuid():N}"[..18],
            DiscoveryRunStartedUtc: DateTimeOffset.UtcNow,
            DiscoveredBy: "user-test");
    }
}
