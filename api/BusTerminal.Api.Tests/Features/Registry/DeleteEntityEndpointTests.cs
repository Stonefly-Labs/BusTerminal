using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry;

// Spec 006 / T065 [US1] [TEST]. Contract test for DELETE /api/registry/{id}.
// Covers happy leaf delete + 409 HasChildren body shape (FR-009).
public sealed class DeleteEntityEndpointTests : IClassFixture<RegistryContractFactory>
{
    private readonly RegistryContractFactory _factory;

    public DeleteEntityEndpointTests(RegistryContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.AuditStore.Clear();
    }

    [Fact]
    public async Task Delete_Leaf_Returns204()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        var created = await client.PostAsJsonAsync("/api/registry", new
        {
            id,
            entityType = "Namespace",
            name = "ns-leaf",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        var etag = created.Headers.ETag!.Tag;

        using var del = new HttpRequestMessage(HttpMethod.Delete, $"/api/registry/{id}");
        del.Headers.Add("If-Match", etag);
        var response = await client.SendAsync(del);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_WithChildren_Returns409_HasChildrenResponse()
    {
        using var client = _factory.CreateClient();
        var nsId = Guid.NewGuid();
        var createdNs = await client.PostAsJsonAsync("/api/registry", new
        {
            id = nsId,
            entityType = "Namespace",
            name = "ns-with-children",
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

        using var del = new HttpRequestMessage(HttpMethod.Delete, $"/api/registry/{nsId}");
        del.Headers.Add("If-Match", createdNs.Headers.ETag!.Tag);
        var response = await client.SendAsync(del);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("code").GetString().Should().Be("HasChildren");
        doc.RootElement.GetProperty("totalChildren").GetInt32().Should().BeGreaterThan(0);
        doc.RootElement.TryGetProperty("childrenByType", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_MissingIfMatch_Returns428()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/registry", new
        {
            id,
            entityType = "Namespace",
            name = "ns-no-etag",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });

        using var del = new HttpRequestMessage(HttpMethod.Delete, $"/api/registry/{id}");
        var response = await client.SendAsync(del);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }
}
