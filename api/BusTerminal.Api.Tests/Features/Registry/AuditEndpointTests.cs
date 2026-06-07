using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry;

// Spec 006 / T116 [US3] [TEST]. Contract test for GET /api/registry/{id}/audit.
// Covers ordering (newest first), the `limit` query param, max-200 clamp /
// rejection, and append-only enforcement (POST/PUT/DELETE/PATCH MUST NOT be
// exposed on this route per FR-034).
public sealed class AuditEndpointTests : IClassFixture<RegistryContractFactory>
{
    private readonly RegistryContractFactory _factory;

    public AuditEndpointTests(RegistryContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.AuditStore.Clear();
    }

    [Fact]
    public async Task Get_ReturnsEventsNewestFirst()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        // Create + two updates → three audit events. Ordering must be newest first.
        var created = await client.PostAsJsonAsync("/api/registry", new
        {
            id,
            entityType = "Namespace",
            name = "audit-order",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        var etag = created.Headers.ETag!.Tag;

        using var firstPut = new HttpRequestMessage(HttpMethod.Put, $"/api/registry/{id}")
        {
            Content = JsonContent.Create(new
            {
                id,
                entityType = "Namespace",
                name = "audit-order",
                environment = "dev",
                status = "Active",
                source = "Manual",
                description = "first edit",
            }),
        };
        firstPut.Headers.Add("If-Match", etag);
        var firstResp = await client.SendAsync(firstPut);
        etag = firstResp.Headers.ETag!.Tag;

        using var secondPut = new HttpRequestMessage(HttpMethod.Put, $"/api/registry/{id}")
        {
            Content = JsonContent.Create(new
            {
                id,
                entityType = "Namespace",
                name = "audit-order",
                environment = "dev",
                status = "Active",
                source = "Manual",
                description = "second edit",
            }),
        };
        secondPut.Headers.Add("If-Match", etag);
        await client.SendAsync(secondPut);

        var response = await client.GetAsync($"/api/registry/{id}/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("items");
        items.GetArrayLength().Should().Be(3);

        var timestamps = new List<DateTimeOffset>();
        foreach (var item in items.EnumerateArray())
        {
            timestamps.Add(item.GetProperty("timestamp").GetDateTimeOffset());
        }
        timestamps.Should().BeInDescendingOrder();

        // Newest event is the second update (Updated event type).
        items[0].GetProperty("eventType").GetString().Should().Be("Updated");
        // Oldest event is the create.
        items[items.GetArrayLength() - 1].GetProperty("eventType").GetString().Should().Be("Created");
    }

    [Fact]
    public async Task Get_RespectsLimitQueryParam()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        var created = await client.PostAsJsonAsync("/api/registry", new
        {
            id,
            entityType = "Namespace",
            name = "audit-limit",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        var etag = created.Headers.ETag!.Tag;

        for (var i = 0; i < 3; i++)
        {
            using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/registry/{id}")
            {
                Content = JsonContent.Create(new
                {
                    id,
                    entityType = "Namespace",
                    name = "audit-limit",
                    environment = "dev",
                    status = "Active",
                    source = "Manual",
                    description = $"edit {i}",
                }),
            };
            put.Headers.Add("If-Match", etag);
            var resp = await client.SendAsync(put);
            etag = resp.Headers.ETag!.Tag;
        }

        var response = await client.GetAsync($"/api/registry/{id}/audit?limit=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    [InlineData(1000)]
    public async Task Get_OutOfRangeLimit_Returns400(int limit)
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        var response = await client.GetAsync($"/api/registry/{id}/audit?limit={limit}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_NoLimit_DefaultsTo50()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        var created = await client.PostAsJsonAsync("/api/registry", new
        {
            id,
            entityType = "Namespace",
            name = "audit-default",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        var etag = created.Headers.ETag!.Tag;

        // Produce 60 audit events total (1 create + 59 updates) so we can prove
        // the default cap is 50.
        for (var i = 0; i < 59; i++)
        {
            using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/registry/{id}")
            {
                Content = JsonContent.Create(new
                {
                    id,
                    entityType = "Namespace",
                    name = "audit-default",
                    environment = "dev",
                    status = "Active",
                    source = "Manual",
                    description = $"edit {i}",
                }),
            };
            put.Headers.Add("If-Match", etag);
            var resp = await client.SendAsync(put);
            etag = resp.Headers.ETag!.Tag;
        }

        var response = await client.GetAsync($"/api/registry/{id}/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(50);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task Audit_WriteVerbs_NotExposed(string verb)
    {
        // FR-034 — append-only. The API MUST NOT expose any write surface on
        // /api/registry/{id}/audit (no POST/PUT/DELETE/PATCH routes).
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        using var req = new HttpRequestMessage(new HttpMethod(verb), $"/api/registry/{id}/audit")
        {
            Content = JsonContent.Create(new { dummy = true }),
        };
        var response = await client.SendAsync(req);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_UnknownEntity_Returns200_WithEmptyItems()
    {
        // Audit list is entity-scoped but tolerant: querying an unknown id
        // returns an empty list rather than 404 so the UI can show "no events"
        // without a second round-trip.
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/registry/{Guid.NewGuid()}/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }
}
