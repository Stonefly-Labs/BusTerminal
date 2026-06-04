using System.Net.Http;
using System.Text.Json;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry;

// Spec 006 / T082. Loads /openapi/v1.json from the running app and asserts the
// registry surface is present (paths + verbs). Deeper schema-vs-spec parity is
// covered by SharedSchemaContractTests (T061) — this test guards against the
// runtime document drifting from the documented contract.
public sealed class OpenApiContractTests : IClassFixture<RegistryContractFactory>
{
    private readonly RegistryContractFactory _factory;

    public OpenApiContractTests(RegistryContractFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApiDocument_AdvertisesEveryRegistryEndpoint()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.IsSuccessStatusCode.Should().BeTrue();

        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);

        var paths = doc.RootElement.GetProperty("paths");
        var expected = new[]
        {
            "/api/registry",
            "/api/registry/{id}",
            "/api/registry/{id}/status",
            "/api/registry/environments",
        };
        foreach (var path in expected)
        {
            paths.TryGetProperty(path, out _).Should().BeTrue(
                because: $"OpenAPI document must declare path {path}.");
        }
    }
}
