using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry;

// Spec 006 / T063 [US1] [TEST]. Contract test for GET /api/registry/{id}.
// Covers happy path, 404 on miss, AND tombstone-exclusion (a deleted entity
// MUST surface as 404 even if a tombstone document remains in storage).
public sealed class GetEntityEndpointTests : IClassFixture<RegistryContractFactory>
{
    private readonly RegistryContractFactory _factory;

    public GetEntityEndpointTests(RegistryContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.AuditStore.Clear();
    }

    [Fact]
    public async Task Get_Returns200_WithEtag()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/registry", new
        {
            id,
            entityType = "Namespace",
            name = "orders-prod",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });

        var response = await client.GetAsync($"/api/registry/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("id").GetGuid().Should().Be(id);
    }

    [Fact]
    public async Task Get_Missing_Returns404()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/registry/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_AfterDelete_Returns404_NotTombstone()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        var created = await client.PostAsJsonAsync("/api/registry", new
        {
            id,
            entityType = "Namespace",
            name = "orders-tomb",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        var etag = created.Headers.ETag!.Tag;

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/registry/{id}");
        deleteRequest.Headers.Add("If-Match", etag);
        var deleteResp = await client.SendAsync(deleteRequest);
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await client.GetAsync($"/api/registry/{id}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
