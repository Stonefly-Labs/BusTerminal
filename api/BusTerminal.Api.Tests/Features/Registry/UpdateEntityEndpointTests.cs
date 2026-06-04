using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry;

// Spec 006 / T064 [US1] [TEST]. Contract test for PUT /api/registry/{id}.
// Covers happy path, ConcurrencyConflict body shape (FR-020), and the
// ForceOverwriteWithoutConflict 400 rejection (data-model.md §3.3).
public sealed class UpdateEntityEndpointTests : IClassFixture<RegistryContractFactory>
{
    private readonly RegistryContractFactory _factory;

    public UpdateEntityEndpointTests(RegistryContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.AuditStore.Clear();
    }

    [Fact]
    public async Task Put_HappyPath_Returns200_WithNewEtag()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        var created = await client.PostAsJsonAsync("/api/registry", new
        {
            id,
            entityType = "Namespace",
            name = "ns-update",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        var initialEtag = created.Headers.ETag!.Tag;

        using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/registry/{id}")
        {
            Content = JsonContent.Create(new
            {
                id,
                entityType = "Namespace",
                name = "ns-update",
                environment = "dev",
                status = "Active",
                source = "Manual",
                description = "updated description",
            }),
        };
        put.Headers.Add("If-Match", initialEtag);

        var response = await client.SendAsync(put);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        response.Headers.ETag!.Tag.Should().NotBe(initialEtag);
    }

    [Fact]
    public async Task Put_StaleEtag_Returns409_WithConflictResponseShape()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        var created = await client.PostAsJsonAsync("/api/registry", new
        {
            id,
            entityType = "Namespace",
            name = "ns-conflict",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        var initialEtag = created.Headers.ETag!.Tag;

        // First update — invalidates the initial etag.
        using var firstPut = new HttpRequestMessage(HttpMethod.Put, $"/api/registry/{id}")
        {
            Content = JsonContent.Create(new
            {
                id,
                entityType = "Namespace",
                name = "ns-conflict",
                environment = "dev",
                status = "Active",
                source = "Manual",
                description = "first writer",
            }),
        };
        firstPut.Headers.Add("If-Match", initialEtag);
        await client.SendAsync(firstPut);

        // Second update reusing the stale etag — should 409.
        using var stalePut = new HttpRequestMessage(HttpMethod.Put, $"/api/registry/{id}")
        {
            Content = JsonContent.Create(new
            {
                id,
                entityType = "Namespace",
                name = "ns-conflict",
                environment = "dev",
                status = "Active",
                source = "Manual",
                description = "second writer (stale)",
            }),
        };
        stalePut.Headers.Add("If-Match", initialEtag);

        var conflict = await client.SendAsync(stalePut);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await conflict.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("code").GetString().Should().Be("ConcurrencyConflict");
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(409);
        doc.RootElement.TryGetProperty("currentEntity", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("changedFields", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Put_ForceOverwriteWithoutConflict_Returns400()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        var created = await client.PostAsJsonAsync("/api/registry", new
        {
            id,
            entityType = "Namespace",
            name = "ns-force",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        var etag = created.Headers.ETag!.Tag;

        using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/registry/{id}")
        {
            Content = JsonContent.Create(new
            {
                id,
                entityType = "Namespace",
                name = "ns-force",
                environment = "dev",
                status = "Active",
                source = "Manual",
                description = "force-flag stuffed",
                _overwriteAcknowledged = true,
            }),
        };
        put.Headers.Add("If-Match", etag);

        var response = await client.SendAsync(put);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("code").GetString().Should().Be("ForceOverwriteWithoutConflict");
    }

    [Fact]
    public async Task Put_MissingIfMatch_Returns428()
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

        using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/registry/{id}")
        {
            Content = JsonContent.Create(new
            {
                id,
                entityType = "Namespace",
                name = "ns-no-etag",
                environment = "dev",
                status = "Active",
                source = "Manual",
            }),
        };

        var response = await client.SendAsync(put);
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }
}
