using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery;

// Spec 009 / T121. Smoke test confirming the published OpenAPI document
// includes every Spec 009 path/operation. .NET 10's Microsoft.AspNetCore.OpenApi
// auto-generates the document from registered endpoints; the test serves as
// a regression gate that the registration in DiscoveryEndpointsBuilder stays
// in sync with the contract under specs/009-entity-discovery-publication/contracts/openapi.yaml.
public sealed class OpenApiSurfaceTests : IClassFixture<DiscoveryContractFactory>
{
    private readonly DiscoveryContractFactory _factory;

    public OpenApiSurfaceTests(DiscoveryContractFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApiDocument_SurfacesEverySpec009Endpoint()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var paths = doc.RootElement.GetProperty("paths");

        // Spec 009 endpoints — verbatim from contracts/openapi.yaml.
        var expected = new (string Path, string Method)[]
        {
            ("/api/namespaces/{namespaceId}/discover", "post"),
            ("/api/discovery-runs/{discoveryRunId}", "get"),
            ("/api/namespaces/{namespaceId}/discovery-runs", "get"),
            ("/api/entities", "get"),
            ("/api/entities/{entityId}", "get"),
            ("/api/entities/{entityId}", "patch"),
            ("/api/entities/{entityId}/archive", "post"),
            ("/api/entities/{entityId}/associations", "get"),
            ("/api/entities/{entityId}/associations", "post"),
            ("/api/entities/{entityId}/associations/{associationId}", "delete"),
        };

        foreach (var (path, method) in expected)
        {
            paths.TryGetProperty(path, out var pathEl)
                .Should().BeTrue($"OpenAPI document should expose {method.ToUpperInvariant()} {path}");
            pathEl.TryGetProperty(method, out _)
                .Should().BeTrue($"path {path} should expose the {method.ToUpperInvariant()} verb");
        }
    }
}
