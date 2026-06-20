using System.Net;
using System.Net.Http;
using System.Text.Json;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.SearchEntities;

// Spec 009 / T061. Contract test for GET /api/entities.
// Covers: query forwarding, multi-value enum parsing, sort token mapping,
// page-size clamping, 503 fallback, and the OpenAPI-defined response shape.
public sealed class SearchEntitiesContractTests : IClassFixture<DiscoveryContractFactory>
{
    private readonly DiscoveryContractFactory _factory;

    public SearchEntitiesContractTests(DiscoveryContractFactory factory)
    {
        _factory = factory;
        _factory.PublishedEntitySearch.Reset();
    }

    [Fact]
    public async Task Search_HappyPath_ReturnsItems_AndForwardsFilters()
    {
        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(
            new[]
            {
                new PublishedEntitySearchHit(
                    Id: "pe_AAAAAAAAAAAAAAAAAAAAAAAA",
                    EntityType: EntityType.Queue,
                    NamespaceId: "ns_test",
                    Name: "orders-inbox",
                    ParentEntityId: null,
                    LifecycleStatus: LifecycleStatus.Active,
                    LastSeenUtc: DateTimeOffset.UtcNow,
                    Environment: "dev",
                    AssociatedServiceIds: new[] { "svc_alpha" },
                    AssociationRoles: new[] { EntityServiceRole.Owner },
                    Tags: new[] { "domain:orders" }),
            },
            TotalCount: 1);

        using var client = AuthedClient();
        var response = await client.GetAsync(
            "/api/entities?q=orders&entityType=Queue&entityType=Topic&namespaceId=ns_test" +
            "&associatedServiceId=svc_alpha&associationRole=Owner&associationRole=Consumer" +
            "&lifecycleStatus=Active&tag=domain:orders&sort=lastSeen_desc&page=2&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJson(response);
        body.GetProperty("items").GetArrayLength().Should().Be(1);
        body.GetProperty("totalCount").GetInt64().Should().Be(1);
        body.GetProperty("page").GetInt32().Should().Be(2);
        body.GetProperty("pageSize").GetInt32().Should().Be(10);

        var first = body.GetProperty("items")[0];
        first.GetProperty("id").GetString().Should().Be("pe_AAAAAAAAAAAAAAAAAAAAAAAA");
        first.GetProperty("entityType").GetString().Should().Be("Queue");
        first.GetProperty("lifecycleStatus").GetString().Should().Be("Active");
        first.GetProperty("associatedServiceIds")[0].GetString().Should().Be("svc_alpha");
        first.GetProperty("associationRoles")[0].GetString().Should().Be("Owner");

        var last = _factory.PublishedEntitySearch.LastRequest;
        last.Should().NotBeNull();
        last!.Query.Should().Be("orders");
        last.EntityTypeFilters.Should().BeEquivalentTo(new[] { EntityType.Queue, EntityType.Topic });
        last.NamespaceIdFilter.Should().Be("ns_test");
        last.AssociatedServiceIdFilter.Should().Be("svc_alpha");
        last.AssociationRoleFilters.Should().BeEquivalentTo(new[] { EntityServiceRole.Owner, EntityServiceRole.Consumer });
        last.LifecycleStatusFilters.Should().BeEquivalentTo(new[] { LifecycleStatus.Active });
        last.TagFilters.Should().BeEquivalentTo(new[] { "domain:orders" });
        last.Sort.Should().Be(PublishedEntitySearchSort.LastSeenDesc);
        last.Skip.Should().Be(10);
        last.Top.Should().Be(10);
    }

    [Fact]
    public async Task Search_NoFilters_DefaultsApply()
    {
        using var client = AuthedClient();
        var response = await client.GetAsync("/api/entities");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var last = _factory.PublishedEntitySearch.LastRequest!;
        last.Sort.Should().Be(PublishedEntitySearchSort.NameAsc);
        last.Skip.Should().Be(0);
        last.Top.Should().Be(25);
        last.EntityTypeFilters.Should().BeNull();
        last.LifecycleStatusFilters.Should().BeNull();
    }

    [Fact]
    public async Task Search_PageSize_ClampedToMax()
    {
        using var client = AuthedClient();
        var response = await client.GetAsync("/api/entities?pageSize=9999");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.PublishedEntitySearch.LastRequest!.Top.Should().Be(100);
    }

    [Fact]
    public async Task Search_InvalidEntityType_Returns400()
    {
        using var client = AuthedClient();
        var response = await client.GetAsync("/api/entities?entityType=Garbage");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJson(response);
        body.GetProperty("code").GetString().Should().Be("InvalidEntityType");
    }

    [Fact]
    public async Task Search_InvalidLifecycleStatus_Returns400()
    {
        using var client = AuthedClient();
        var response = await client.GetAsync("/api/entities?lifecycleStatus=Vibes");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJson(response);
        body.GetProperty("code").GetString().Should().Be("InvalidLifecycleStatus");
    }

    [Fact]
    public async Task Search_InvalidAssociationRole_Returns400()
    {
        using var client = AuthedClient();
        var response = await client.GetAsync("/api/entities?associationRole=BadActor");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJson(response);
        body.GetProperty("code").GetString().Should().Be("InvalidAssociationRole");
    }

    [Theory]
    [InlineData("name_asc", PublishedEntitySearchSort.NameAsc)]
    [InlineData("name_desc", PublishedEntitySearchSort.NameDesc)]
    [InlineData("lastSeen_asc", PublishedEntitySearchSort.LastSeenAsc)]
    [InlineData("lastSeen_desc", PublishedEntitySearchSort.LastSeenDesc)]
    public async Task Search_SortToken_MapsToAdapterSort(string token, PublishedEntitySearchSort expected)
    {
        using var client = AuthedClient();
        var response = await client.GetAsync($"/api/entities?sort={token}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.PublishedEntitySearch.LastRequest!.Sort.Should().Be(expected);
    }

    [Fact]
    public async Task Search_AiSearchOutage_Returns503_WithProblemJson()
    {
        _factory.PublishedEntitySearch.ThrowOnSearch = true;
        using var client = AuthedClient();
        var response = await client.GetAsync("/api/entities?q=orders");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await ReadJson(response);
        body.GetProperty("code").GetString().Should().Be("SearchUnavailable");
    }

    [Fact]
    public async Task Search_Pagination_ComputesSkipFromPage()
    {
        using var client = AuthedClient();
        var response = await client.GetAsync("/api/entities?page=3&pageSize=25");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.PublishedEntitySearch.LastRequest!.Skip.Should().Be(50);
        _factory.PublishedEntitySearch.LastRequest.Top.Should().Be(25);
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
