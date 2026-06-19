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

namespace BusTerminal.Api.Tests.Features.Discovery.UpdateEntityMetadata;

// Spec 009 / T096 + FR-016. End-to-end (store-layer) integration test.
// Curated metadata applied via UpdateCuratedMetadataAsync MUST survive
// a subsequent discovery upsert (UpsertAzureSourcedAsync) unchanged. Tests
// against the live Cosmos endpoint described by the RegistryFixture; skips
// cleanly when BUSTERMINAL_TEST_COSMOS_ENDPOINT is unset.
[Collection("RegistryFixture")]
[Trait("Category", "Integration")]
public sealed class MetadataPreservationIntegrationTests
{
    private readonly RegistryFixture _fixture;

    public MetadataPreservationIntegrationTests(RegistryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CuratedFields_SurviveDiscoveryRediscovery()
    {
        if (!_fixture.ShouldRun()) return;

        var store = CreateStore();
        var env = _fixture.Environment;
        var ns = $"ns_test_{Guid.NewGuid():N}";
        var entityId = PublishedEntityIdComputer.ComputeFor(EntityType.Queue, ns, leafName: "orders");

        // 1. Discovery seeds the entity.
        var initial = NewUpsert(entityId, env, ns, "orders");
        await store.UpsertAzureSourcedAsync(initial, ifMatch: null, CancellationToken.None);

        // 2. Curate the entity via the spec 009 PATCH path.
        var afterCreate = await store.GetDetailAsync(entityId, env, CancellationToken.None);
        afterCreate.Should().NotBeNull();
        var curated = await store.UpdateCuratedMetadataAsync(
            entityId, env,
            new CuratedMetadataPatch(
                Description: OptionalValue<string?>.Set("Curated description"),
                BusinessPurpose: OptionalValue<string?>.Set("Bridges checkout"),
                Tags: OptionalValue<IReadOnlyList<string>?>.Set(new[] { "domain:orders" }),
                DocumentationLinks: OptionalValue<IReadOnlyList<EntityDocumentationLink>?>.Unset(),
                ContactInformation: OptionalValue<EntityContactInformation?>.Set(
                    new EntityContactInformation("fulfillment-team@example.com", null)),
                OperationalNotes: OptionalValue<string?>.Set("Drains via worker-job")),
            afterCreate!.ETag,
            modifiedBy: "operator-test",
            CancellationToken.None);
        curated.Entity.Registry.Description.Should().Be("Curated description");

        // 3. Add a service association.
        var association = new EntityServiceAssociation(
            AssociationId: "esa_int_test",
            ServiceId: "svc_payments",
            Role: EntityServiceRole.Owner,
            CreatedUtc: DateTimeOffset.UtcNow,
            CreatedBy: "operator-test");
        var afterAssoc = await store.AddAssociationAsync(
            entityId, env, association, curated.ETag, modifiedBy: "operator-test", CancellationToken.None);
        afterAssoc.Detail.Entity.ServiceAssociations.Should().HaveCount(1);

        // 4. Re-run discovery — must NOT clobber curated fields.
        var rediscover = initial with
        {
            AzureSourcedHash = "sha256:rediscovered",
            DiscoveryRunId = $"dr_{Guid.NewGuid():N}"[..18],
            DiscoveryRunStartedUtc = DateTimeOffset.UtcNow.AddSeconds(60),
        };
        await store.UpsertAzureSourcedAsync(rediscover, ifMatch: null, CancellationToken.None);

        var afterRediscovery = await store.GetDetailAsync(entityId, env, CancellationToken.None);
        afterRediscovery.Should().NotBeNull();
        afterRediscovery!.Entity.Registry.Description.Should().Be("Curated description");
        afterRediscovery.Entity.Registry.BusinessPurpose.Should().Be("Bridges checkout");
        afterRediscovery.Entity.Registry.Tags.Should().ContainSingle().Which.Should().Be("domain:orders");
        afterRediscovery.Entity.Registry.OperationalNotes.Should().Be("Drains via worker-job");
        afterRediscovery.Entity.ServiceAssociations.Should().HaveCount(1);
        afterRediscovery.Entity.ServiceAssociations[0].ServiceId.Should().Be("svc_payments");
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
