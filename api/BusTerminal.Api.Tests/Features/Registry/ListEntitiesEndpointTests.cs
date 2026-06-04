using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry;

// Spec 006 / T066 [US1] [TEST]. Contract test for GET /api/registry (list).
// Covers env-scoped pagination, entityType / parentId filters, AND tombstone
// exclusion (no document with `_isTombstone = true` may appear in list output).
public sealed class ListEntitiesEndpointTests : IClassFixture<RegistryContractFactory>
{
    private readonly RegistryContractFactory _factory;

    public ListEntitiesEndpointTests(RegistryContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.AuditStore.Clear();
    }

    [Fact]
    public async Task List_WithoutEnvironment_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/registry");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_EnvScoped_ReturnsEntities()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/registry", new
        {
            id = Guid.NewGuid(),
            entityType = "Namespace",
            name = "ns-list",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        await client.PostAsJsonAsync("/api/registry", new
        {
            id = Guid.NewGuid(),
            entityType = "Namespace",
            name = "ns-list-other",
            environment = "prod",
            status = "Active",
            source = "Manual",
        });

        var response = await client.GetAsync("/api/registry?environment=dev");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.GetProperty("items").EnumerateArray().ToArray();
        items.Should().HaveCount(1);
        items[0].GetProperty("environment").GetString().Should().Be("dev");
    }

    [Fact]
    public async Task List_FilterByEntityTypeAndParentId_NarrowsResults()
    {
        using var client = _factory.CreateClient();
        var nsId = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/registry", new
        {
            id = nsId,
            entityType = "Namespace",
            name = "ns-parent",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        await client.PostAsJsonAsync("/api/registry", new
        {
            id = Guid.NewGuid(),
            entityType = "Queue",
            name = "q-1",
            environment = "dev",
            status = "Active",
            source = "Manual",
            parentId = nsId,
        });
        await client.PostAsJsonAsync("/api/registry", new
        {
            id = Guid.NewGuid(),
            entityType = "Topic",
            name = "t-1",
            environment = "dev",
            status = "Active",
            source = "Manual",
            parentId = nsId,
        });

        var queuesOnly = await client.GetAsync($"/api/registry?environment=dev&entityType=Queue&parentId={nsId}");
        queuesOnly.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await queuesOnly.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.GetProperty("items").EnumerateArray().ToArray();
        items.Should().HaveCount(1);
        items[0].GetProperty("entityType").GetString().Should().Be("Queue");
    }

    [Fact]
    public async Task List_AfterDelete_ExcludesDeleted()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        var created = await client.PostAsJsonAsync("/api/registry", new
        {
            id,
            entityType = "Namespace",
            name = "ns-vanish",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        using var del = new HttpRequestMessage(HttpMethod.Delete, $"/api/registry/{id}");
        del.Headers.Add("If-Match", created.Headers.ETag!.Tag);
        await client.SendAsync(del);

        var response = await client.GetAsync("/api/registry?environment=dev");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("items").EnumerateArray()
            .Should().NotContain(e => e.GetProperty("id").GetGuid() == id);
    }
}
