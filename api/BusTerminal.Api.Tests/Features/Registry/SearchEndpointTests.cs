using System.Net;
using System.Text.Json;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry;

// Spec 006 / T104 [US2] [TEST]. Contract test for GET /api/registry/search.
// Covers: query validation, filter/sort variants, pagination, and the 503
// fallback when AI Search is unavailable.
public sealed class SearchEndpointTests : IClassFixture<RegistryContractFactory>
{
    private readonly RegistryContractFactory _factory;

    public SearchEndpointTests(RegistryContractFactory factory)
    {
        _factory = factory;
        _factory.SearchClient.Reset();
    }

    [Fact]
    public async Task Search_MissingQuery_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/registry/search");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("code").GetString().Should().Be("QueryRequired");
    }

    [Fact]
    public async Task Search_TagValueWithoutKey_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/registry/search?q=orders&tagValue=foo");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("code").GetString().Should().Be("TagValueRequiresKey");
    }

    [Fact]
    public async Task Search_HappyPath_ReturnsRankedItems()
    {
        _factory.SearchClient.NextResults = new RegistrySearchResults(
            Hits: new[]
            {
                new RegistrySearchHit(
                    Id: Guid.NewGuid(),
                    EntityType: RegistryEntityType.Queue,
                    Name: "orders-incoming",
                    FullyQualifiedName: "orders-prod/orders-incoming",
                    Environment: "dev",
                    Status: "Active",
                    Owner: "payments-platform",
                    NamespaceName: "orders-prod",
                    ParentId: Guid.NewGuid(),
                    Score: 1.42),
            },
            TotalCount: 1);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync(
            "/api/registry/search?q=orders&entityType=Queue&environment=dev&status=Active&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("totalCount").GetInt64().Should().Be(1);
        doc.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("pageSize").GetInt32().Should().Be(10);
        var first = doc.RootElement.GetProperty("items")[0];
        first.GetProperty("entityType").GetString().Should().Be("Queue");
        first.GetProperty("name").GetString().Should().Be("orders-incoming");

        // The endpoint forwards the parsed request to the adapter.
        _factory.SearchClient.LastRequest.Should().NotBeNull();
        _factory.SearchClient.LastRequest!.EntityTypeFilter.Should().Be(RegistryEntityType.Queue);
        _factory.SearchClient.LastRequest.EnvironmentFilter.Should().Be("dev");
        _factory.SearchClient.LastRequest.StatusFilter.Should().Be(RegistryEntityStatus.Active);
        _factory.SearchClient.LastRequest.Top.Should().Be(10);
        _factory.SearchClient.LastRequest.Skip.Should().Be(0);
    }

    [Theory]
    [InlineData("name_asc", RegistrySearchSort.NameAsc)]
    [InlineData("updated_desc", RegistrySearchSort.UpdatedAtDesc)]
    [InlineData("relevance", RegistrySearchSort.Relevance)]
    public async Task Search_SortToken_MapsToAdapterSort(string token, RegistrySearchSort expected)
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/registry/search?q=orders&sort={token}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.SearchClient.LastRequest!.Sort.Should().Be(expected);
    }

    [Fact]
    public async Task Search_TagKey_OnlySetsLowercaseKeyFilter()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/registry/search?q=*&tagKey=Owner");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.SearchClient.LastRequest!.TagKeysAnyLower.Should().BeEquivalentTo(new[] { "owner" });
        _factory.SearchClient.LastRequest.TagsAny.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Search_TagKeyAndValue_SetsTagsAnyFilter()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/registry/search?q=*&tagKey=Owner&tagValue=PaymentsTeam");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.SearchClient.LastRequest!.TagsAny.Should().NotBeNullOrEmpty();
        var tag = _factory.SearchClient.LastRequest.TagsAny!.Single();
        tag.Key.Should().Be("Owner");
        tag.Value.Should().Be("PaymentsTeam");
    }

    [Fact]
    public async Task Search_Pagination_ComputesSkipFromPage()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/registry/search?q=*&page=3&pageSize=25");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.SearchClient.LastRequest!.Skip.Should().Be(50);
        _factory.SearchClient.LastRequest.Top.Should().Be(25);
    }

    [Fact]
    public async Task Search_PageSize_ClampedToMax()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/registry/search?q=*&pageSize=9999");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.SearchClient.LastRequest!.Top.Should().Be(100);
    }

    [Fact]
    public async Task Search_AiSearchOutage_Returns503_WithRfc7807Body()
    {
        _factory.SearchClient.ThrowOnSearch = true;
        _factory.SearchClient.ThrowStatus = 503;

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/registry/search?q=orders");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("code").GetString().Should().Be("SearchUnavailable");
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(503);
    }

    [Fact]
    public async Task Search_InvalidEntityType_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/registry/search?q=orders&entityType=NotARealType");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
