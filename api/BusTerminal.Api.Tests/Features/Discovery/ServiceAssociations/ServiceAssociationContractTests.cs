using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Infrastructure.Authentication;
using BusTerminal.Api.Tests.Features.Discovery.Shared;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.ServiceAssociations;

// Spec 009 / T095. Contract tests for the M:N association surface:
//   GET    /api/entities/{id}/associations
//   POST   /api/entities/{id}/associations              (+ 409 dup)
//   DELETE /api/entities/{id}/associations/{assocId}
public sealed class ServiceAssociationContractTests : IClassFixture<DiscoveryContractFactory>
{
    private const string EntityId = "pe_CCCCCCCCCCCCCCCCCCCCCCCC";
    private readonly DiscoveryContractFactory _factory;

    public ServiceAssociationContractTests(DiscoveryContractFactory factory)
    {
        _factory = factory;
        _factory.PublishedEntitySearch.Reset();
        _factory.OwnedServices.Reset();
    }

    // ── GET ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsCurrentAssociations()
    {
        var association = new EntityServiceAssociation(
            AssociationId: "esa_existing",
            ServiceId: "svc_alpha",
            Role: EntityServiceRole.Owner,
            CreatedUtc: DateTimeOffset.UtcNow,
            CreatedBy: "00000000-0000-0000-0000-000000000099");
        Seed("\"etag-1\"", new[] { association });

        using var client = ReaderClient();
        var response = await client.GetAsync($"/api/entities/{EntityId}/associations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJson(response);
        body.GetArrayLength().Should().Be(1);
        body[0].GetProperty("serviceId").GetString().Should().Be("svc_alpha");
        body[0].GetProperty("role").GetString().Should().Be("Owner");
    }

    [Fact]
    public async Task List_UnknownEntity_Returns404()
    {
        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(
            Array.Empty<PublishedEntitySearchHit>(), 0);
        using var client = ReaderClient();
        var response = await client.GetAsync($"/api/entities/{EntityId}/associations");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_NoIfMatch_Returns428()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var response = await client.PostAsJsonAsync($"/api/entities/{EntityId}/associations",
            new { serviceId = "svc_new", role = "Consumer" });
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task Add_HappyPath_Returns201_WithAssociation()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var request = BuildPost(ifMatch: "\"etag-1\"",
            body: JsonSerializer.Serialize(new { serviceId = "svc_new", role = "Consumer" }));

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.ETag.Should().NotBeNull();

        var body = await ReadJson(response);
        body.GetProperty("associationId").GetString().Should().StartWith("esa_");
        body.GetProperty("serviceId").GetString().Should().Be("svc_new");
        body.GetProperty("role").GetString().Should().Be("Consumer");
    }

    [Fact]
    public async Task Add_DuplicateTriple_Returns409()
    {
        var existing = new EntityServiceAssociation(
            AssociationId: "esa_pre",
            ServiceId: "svc_alpha",
            Role: EntityServiceRole.Owner,
            CreatedUtc: DateTimeOffset.UtcNow,
            CreatedBy: "00000000-0000-0000-0000-000000000099");
        Seed("\"etag-1\"", new[] { existing });

        using var client = AdminClient();
        var request = BuildPost(ifMatch: "\"etag-1\"",
            body: JsonSerializer.Serialize(new { serviceId = "svc_alpha", role = "Owner" }));

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await ReadJson(response);
        body.GetProperty("code").GetString().Should().Be("DuplicateAssociation");
    }

    [Fact]
    public async Task Add_SameServiceDifferentRole_Allowed()
    {
        var existing = new EntityServiceAssociation(
            AssociationId: "esa_pre",
            ServiceId: "svc_alpha",
            Role: EntityServiceRole.Owner,
            CreatedUtc: DateTimeOffset.UtcNow,
            CreatedBy: "00000000-0000-0000-0000-000000000099");
        Seed("\"etag-1\"", new[] { existing });

        using var client = AdminClient();
        var request = BuildPost(ifMatch: "\"etag-1\"",
            body: JsonSerializer.Serialize(new { serviceId = "svc_alpha", role = "Producer" }));

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Add_MissingServiceId_Returns400()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var request = BuildPost(ifMatch: "\"etag-1\"",
            body: JsonSerializer.Serialize(new { role = "Consumer" }));

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_CallerWithoutAuthority_Returns403()
    {
        Seed("\"etag-1\"");
        using var client = ReaderClient();
        var request = BuildPost(ifMatch: "\"etag-1\"",
            body: JsonSerializer.Serialize(new { serviceId = "svc_new", role = "Consumer" }));

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Remove_HappyPath_Returns204()
    {
        var existing = new EntityServiceAssociation(
            AssociationId: "esa_existing",
            ServiceId: "svc_alpha",
            Role: EntityServiceRole.Owner,
            CreatedUtc: DateTimeOffset.UtcNow,
            CreatedBy: "00000000-0000-0000-0000-000000000099");
        Seed("\"etag-1\"", new[] { existing });

        using var client = AdminClient();
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/entities/{EntityId}/associations/{existing.AssociationId}");
        request.Headers.TryAddWithoutValidation("If-Match", "\"etag-1\"");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.ETag.Should().NotBeNull();
    }

    [Fact]
    public async Task Remove_UnknownAssociation_Returns404()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/entities/{EntityId}/associations/esa_nope");
        request.Headers.TryAddWithoutValidation("If-Match", "\"etag-1\"");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await ReadJson(response);
        body.GetProperty("code").GetString().Should().Be("AssociationNotFound");
    }

    [Fact]
    public async Task Remove_NoIfMatch_Returns428()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var response = await client.DeleteAsync($"/api/entities/{EntityId}/associations/esa_pre");
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    private void Seed(string etag, IReadOnlyList<EntityServiceAssociation>? associations = null)
    {
        var (detail, hit) = PublishedEntitySeed.Build(id: EntityId, etag: etag, associations: associations);
        _factory.PublishedEntities.Seed(detail);
        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(new[] { hit }, 1);
    }

    private HttpRequestMessage BuildPost(string ifMatch, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/entities/{EntityId}/associations")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return request;
    }

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(MockAuthenticationHandler.MockRolesHeader, "BusTerminal.Admin");
        return client;
    }

    private HttpClient ReaderClient()
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
