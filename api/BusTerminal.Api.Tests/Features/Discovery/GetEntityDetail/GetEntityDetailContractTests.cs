using System.Net;
using System.Net.Http;
using System.Text.Json;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.GetEntityDetail;

// Spec 009 / T062. Contract test for GET /api/entities/{entityId}.
// Verifies the two-step resolve (search → environment → Cosmos read),
// 404 paths, and the ETag/Last-Modified response headers.
public sealed class GetEntityDetailContractTests : IClassFixture<DiscoveryContractFactory>
{
    private readonly DiscoveryContractFactory _factory;

    public GetEntityDetailContractTests(DiscoveryContractFactory factory)
    {
        _factory = factory;
        _factory.PublishedEntitySearch.Reset();
    }

    [Fact]
    public async Task GetDetail_ExistingEntity_Returns200_AndEchoesETag()
    {
        const string id = "pe_AAAAAAAAAAAAAAAAAAAAAAAA";
        const string env = "dev";

        var azureSourced = new AzureSourcedQueue(
            AzureResourceId: "/subscriptions/x/queues/orders-inbox",
            ArmEtag: "W/\"abc\"",
            Status: "Active",
            LockDuration: "PT1M",
            MaxDeliveryCount: 10,
            DuplicateDetection: new AzureSourcedDuplicateDetection(true, "PT10M"),
            DeadLettering: new AzureSourcedDeadLettering(true),
            Partitioning: new AzureSourcedPartitioning(false),
            Session: new AzureSourcedSession(false),
            Forwarding: new AzureSourcedForwarding(null, null),
            DefaultTimeToLive: "P14D",
            MaxSizeInMegabytes: 5120);

        var now = DateTimeOffset.UtcNow;
        var entity = new PublishedEntity(
            Id: id,
            SchemaVersion: PublishedEntity.CurrentSchemaVersion,
            EntityType: EntityType.Queue,
            Environment: env,
            NamespaceId: "ns_test",
            Name: "orders-inbox",
            DisplayName: "orders-inbox",
            CompositeKey: "q:ns_test/orders-inbox",
            ParentEntityId: null,
            Registry: EntityRegistryMetadata.Empty with { Description = "Orders queue" },
            LifecycleStatus: LifecycleStatus.Active,
            LifecycleStatusChangedUtc: now,
            FirstDiscoveredUtc: now,
            LastSeenUtc: now,
            LastDiscoveryRunId: "dr_TEST00000000000000000000001",
            AzureSourced: azureSourced,
            AzureSourcedHash: "sha256:abc",
            ServiceAssociations: Array.Empty<EntityServiceAssociation>(),
            CreatedUtc: now,
            CreatedBy: "00000000-0000-0000-0000-000000000099",
            LastModifiedUtc: now,
            LastModifiedBy: "00000000-0000-0000-0000-000000000099",
            ETag: "\"etag-v1\"");

        _factory.PublishedEntities.Seed(new PublishedEntityDetail(entity, "\"etag-v1\""));

        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(
            new[]
            {
                new PublishedEntitySearchHit(
                    Id: id,
                    EntityType: EntityType.Queue,
                    NamespaceId: "ns_test",
                    Name: "orders-inbox",
                    ParentEntityId: null,
                    LifecycleStatus: LifecycleStatus.Active,
                    LastSeenUtc: now,
                    Environment: env,
                    AssociatedServiceIds: Array.Empty<string>(),
                    AssociationRoles: Array.Empty<EntityServiceRole>(),
                    Tags: Array.Empty<string>()),
            },
            TotalCount: 1);

        using var client = AuthedClient();
        var response = await client.GetAsync($"/api/entities/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        response.Content.Headers.LastModified.Should().NotBeNull();

        var body = await ReadJson(response);
        body.GetProperty("id").GetString().Should().Be(id);
        body.GetProperty("name").GetString().Should().Be("orders-inbox");
        body.GetProperty("environment").GetString().Should().Be(env);
        body.GetProperty("entityType").GetString().Should().Be("Queue");
        body.GetProperty("lifecycleStatus").GetString().Should().Be("Active");
        body.GetProperty("azureSourcedHash").GetString().Should().Be("sha256:abc");
        body.GetProperty("azureSourced").GetProperty("status").GetString().Should().Be("Active");
        body.GetProperty("azureSourced").GetProperty("lockDuration").GetString().Should().Be("PT1M");
    }

    [Fact]
    public async Task GetDetail_UnknownEntity_Returns404()
    {
        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(
            Array.Empty<PublishedEntitySearchHit>(), 0);

        using var client = AuthedClient();
        var response = await client.GetAsync("/api/entities/pe_ZZZZZZZZZZZZZZZZZZZZZZZZ");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDetail_SearchHitsButStoreEmpty_Returns404()
    {
        const string id = "pe_BBBBBBBBBBBBBBBBBBBBBBBB";
        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(
            new[]
            {
                new PublishedEntitySearchHit(
                    Id: id,
                    EntityType: EntityType.Queue,
                    NamespaceId: "ns_test",
                    Name: "ghost",
                    ParentEntityId: null,
                    LifecycleStatus: LifecycleStatus.Active,
                    LastSeenUtc: null,
                    Environment: "dev",
                    AssociatedServiceIds: Array.Empty<string>(),
                    AssociationRoles: Array.Empty<EntityServiceRole>(),
                    Tags: Array.Empty<string>()),
            },
            TotalCount: 1);

        using var client = AuthedClient();
        var response = await client.GetAsync($"/api/entities/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private HttpClient AuthedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(MockAuthenticationHandler.MockRolesHeader, "BusTerminal.Reader");
        return client;
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content).RootElement.Clone();
    }
}
