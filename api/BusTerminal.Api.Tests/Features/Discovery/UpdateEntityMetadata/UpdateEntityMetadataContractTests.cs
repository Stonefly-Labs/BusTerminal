using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Infrastructure.Authentication;
using BusTerminal.Api.Tests.Features.Discovery.Shared;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.UpdateEntityMetadata;

// Spec 009 / T093. Contract test for PATCH /api/entities/{entityId}.
// Verifies:
//   • azureSourced.* (and other discovery-owned fields) rejected with 400.
//   • If-Match enforcement (missing → 428, stale → 412).
//   • Auth: 403 when caller has neither admin role nor owner association.
//   • Happy path: 200 with updated entity + new ETag header.
//   • Partial-update semantics: only present fields touched.
public sealed class UpdateEntityMetadataContractTests : IClassFixture<DiscoveryContractFactory>
{
    private const string EntityId = "pe_AAAAAAAAAAAAAAAAAAAAAAAA";
    private readonly DiscoveryContractFactory _factory;

    public UpdateEntityMetadataContractTests(DiscoveryContractFactory factory)
    {
        _factory = factory;
        _factory.PublishedEntitySearch.Reset();
        _factory.OwnedServices.Reset();
    }

    [Fact]
    public async Task Patch_NoIfMatch_Returns428()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var response = await client.PatchAsync($"/api/entities/{EntityId}",
            new StringContent("{\"description\":\"x\"}", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task Patch_AzureSourcedField_Returns400()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var body = "{\"description\":\"ok\",\"azureSourced\":{\"status\":\"Active\"}}";

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/entities/{EntityId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"etag-1\"");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await ReadJson(response);
        problem.GetProperty("code").GetString().Should().Be("DisallowedField");
    }

    [Fact]
    public async Task Patch_LifecycleStatusField_Returns400()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var body = "{\"description\":\"ok\",\"lifecycleStatus\":\"Archived\"}";

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/entities/{EntityId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"etag-1\"");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await ReadJson(response);
        problem.GetProperty("code").GetString().Should().Be("DisallowedField");
    }

    [Fact]
    public async Task Patch_StaleIfMatch_Returns412()
    {
        Seed("\"etag-current\"");
        using var client = AdminClient();
        var request = BuildPatch(ifMatch: "\"etag-old\"", json: "{\"description\":\"x\"}");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        var problem = await ReadJson(response);
        problem.GetProperty("code").GetString().Should().Be("PreconditionFailed");
    }

    [Fact]
    public async Task Patch_UnknownEntity_Returns404()
    {
        // No seed → search miss.
        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(
            Array.Empty<PublishedEntitySearchHit>(), 0);
        using var client = AdminClient();
        var request = BuildPatch(ifMatch: "\"etag-1\"", json: "{\"description\":\"x\"}");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_CallerWithoutRoleOrOwnership_Returns403()
    {
        Seed("\"etag-1\"");
        using var client = ReaderClient();
        var request = BuildPatch(ifMatch: "\"etag-1\"", json: "{\"description\":\"x\"}");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Patch_NoBody_Returns400()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/entities/{EntityId}")
        {
            Content = new StringContent("", Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"etag-1\"");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_NoFieldsToUpdate_Returns400()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var request = BuildPatch(ifMatch: "\"etag-1\"", json: "{}");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await ReadJson(response);
        problem.GetProperty("code").GetString().Should().Be("NoFieldsToUpdate");
    }

    [Fact]
    public async Task Patch_HappyPath_Returns200_AndEchoesNewETag()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var json = JsonSerializer.Serialize(new
        {
            description = "New description from contract test",
            businessPurpose = "Bridges checkout API to fulfillment.",
            tags = new[] { "domain:orders", "tier:critical" },
            operationalNotes = "Drains via fulfillment-worker-job.",
        });
        var request = BuildPatch(ifMatch: "\"etag-1\"", json: json);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();

        var body = await ReadJson(response);
        body.GetProperty("id").GetString().Should().Be(EntityId);
        body.GetProperty("description").GetString().Should().Be("New description from contract test");
        body.GetProperty("tags").GetArrayLength().Should().Be(2);
        // Azure-sourced fields preserved verbatim:
        body.GetProperty("azureSourced").GetProperty("lockDuration").GetString().Should().Be("PT1M");
    }

    [Fact]
    public async Task Patch_PartialUpdate_OnlyTouchesPresentFields()
    {
        var (detail, hit) = PublishedEntitySeed.Build(
            id: EntityId,
            etag: "\"etag-1\"",
            registry: new EntityRegistryMetadata(
                Description: "Original description",
                BusinessPurpose: "Original purpose",
                Tags: new[] { "original-tag" },
                DocumentationLinks: Array.Empty<EntityDocumentationLink>(),
                ContactInformation: null,
                OperationalNotes: "Original notes"));
        _factory.PublishedEntities.Seed(detail);
        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(new[] { hit }, 1);

        using var client = AdminClient();
        var request = BuildPatch(ifMatch: "\"etag-1\"", json: "{\"description\":\"Updated only\"}");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJson(response);
        body.GetProperty("description").GetString().Should().Be("Updated only");
        // BusinessPurpose untouched.
        body.GetProperty("businessPurpose").GetString().Should().Be("Original purpose");
        body.GetProperty("operationalNotes").GetString().Should().Be("Original notes");
        // Tags untouched.
        body.GetProperty("tags").GetArrayLength().Should().Be(1);
        body.GetProperty("tags")[0].GetString().Should().Be("original-tag");
    }

    [Fact]
    public async Task Patch_NullField_ClearsValue()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var request = BuildPatch(ifMatch: "\"etag-1\"", json: "{\"description\":null}");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJson(response);
        // Cleared description should be null (or absent).
        var hasDesc = body.TryGetProperty("description", out var desc);
        if (hasDesc)
        {
            desc.ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task Patch_InvalidUrl_Returns400()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var json = JsonSerializer.Serialize(new
        {
            documentationLinks = new[] { new { label = "Runbook", url = "not-a-url" } },
        });
        var request = BuildPatch(ifMatch: "\"etag-1\"", json: json);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private void Seed(string etag)
    {
        var (detail, hit) = PublishedEntitySeed.Build(id: EntityId, etag: etag);
        _factory.PublishedEntities.Seed(detail);
        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(new[] { hit }, 1);
    }

    private HttpRequestMessage BuildPatch(string ifMatch, string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/entities/{EntityId}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
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
