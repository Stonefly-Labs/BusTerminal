using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BusTerminal.Api.Tests.Integration;

public sealed class HealthEndpointTests : IClassFixture<HealthAppFactory>
{
    private readonly HealthAppFactory _factory;

    public HealthEndpointTests(HealthAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Liveness_ReturnsOk()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_InDevMockMode_ReturnsHealthy()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<HealthPayload>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task Startup_InDevMockMode_ReturnsHealthy()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz/startup");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Startup_BeforeMetadataReachable_ReturnsServiceUnavailable()
    {
        await using var factory = new UnreachableEntraAppFactory();
        using var client = factory.CreateClient();
        var startup = await client.GetAsync("/healthz/startup");
        startup.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var ready = await client.GetAsync("/healthz/ready");
        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private sealed record HealthPayload(string Status);
}

public sealed class UnreachableEntraAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Production);
        // Real tenant id (not the "development" sentinel) — forces the real Entra path.
        builder.UseSetting("AzureAd:TenantId", "11111111-1111-1111-1111-111111111111");
        // Unreachable instance URL (.invalid TLD per RFC 2606) — metadata fetch will fail.
        builder.UseSetting("AzureAd:Instance", "https://login.invalid/");
        builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000000");
        builder.UseSetting("AzureAd:Audience", "api://busterminal-test");
    }
}

public sealed class HealthAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Development);
        builder.UseSetting("AzureAd:TenantId", "development");
        builder.UseSetting("AzureAd:Instance", "https://login.microsoftonline.com/");
        builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000000");
        builder.UseSetting("AzureAd:Audience", "api://busterminal-dev");
    }
}
