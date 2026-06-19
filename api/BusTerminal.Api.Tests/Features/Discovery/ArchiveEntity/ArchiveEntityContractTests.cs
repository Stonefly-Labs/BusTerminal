using System.Net;
using System.Net.Http;
using System.Text.Json;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Infrastructure.Authentication;
using BusTerminal.Api.Tests.Features.Discovery.Shared;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.ArchiveEntity;

// Spec 009 / T094. Contract test for POST /api/entities/{entityId}/archive.
// Mirrors the UpdateEntityMetadata contract test for shared concerns (auth,
// ETag, 404) and adds the archive-specific shape assertion.
public sealed class ArchiveEntityContractTests : IClassFixture<DiscoveryContractFactory>
{
    private const string EntityId = "pe_BBBBBBBBBBBBBBBBBBBBBBBB";
    private readonly DiscoveryContractFactory _factory;

    public ArchiveEntityContractTests(DiscoveryContractFactory factory)
    {
        _factory = factory;
        _factory.PublishedEntitySearch.Reset();
        _factory.OwnedServices.Reset();
    }

    [Fact]
    public async Task Archive_NoIfMatch_Returns428()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var response = await client.PostAsync($"/api/entities/{EntityId}/archive", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task Archive_StaleIfMatch_Returns412()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/entities/{EntityId}/archive");
        request.Headers.TryAddWithoutValidation("If-Match", "\"etag-old\"");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Archive_CallerWithoutRoleOrOwnership_Returns403()
    {
        Seed("\"etag-1\"");
        using var client = ReaderClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/entities/{EntityId}/archive");
        request.Headers.TryAddWithoutValidation("If-Match", "\"etag-1\"");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Archive_UnknownEntity_Returns404()
    {
        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(
            Array.Empty<PublishedEntitySearchHit>(), 0);
        using var client = AdminClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/entities/{EntityId}/archive");
        request.Headers.TryAddWithoutValidation("If-Match", "\"etag-1\"");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Archive_HappyPath_Returns200_WithArchivedStatus()
    {
        Seed("\"etag-1\"");
        using var client = AdminClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/entities/{EntityId}/archive");
        request.Headers.TryAddWithoutValidation("If-Match", "\"etag-1\"");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();

        var body = await ReadJson(response);
        body.GetProperty("lifecycleStatus").GetString().Should().Be("Archived");
    }

    private void Seed(string etag)
    {
        var (detail, hit) = PublishedEntitySeed.Build(id: EntityId, etag: etag);
        _factory.PublishedEntities.Seed(detail);
        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(new[] { hit }, 1);
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
