using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using BusTerminal.Api.Features.Identity;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BusTerminal.Api.Tests.Integration;

public sealed class WhoAmIEndpointTests : IClassFixture<WhoAmIAppFactory>
{
    private readonly WhoAmIAppFactory _factory;

    public WhoAmIEndpointTests(WhoAmIAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnauthenticatedRequest_Returns401_WithWWWAuthenticate()
    {
        await using var unauthenticatedFactory = new UnauthenticatedFactory();
        using var client = unauthenticatedFactory.CreateClient();

        var response = await client.GetAsync("/whoami");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AuthenticatedRequest_ReturnsExpectedPrincipalShape()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/whoami");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WhoAmIResponse>();
        body.Should().NotBeNull();
        body!.Principal.Oid.Should().Be(MockAuthenticationHandler.DevPrincipalOid);
        body.Principal.DisplayName.Should().Be(MockAuthenticationHandler.DevPrincipalDisplayName);
        body.Principal.PreferredUsername.Should().Be(MockAuthenticationHandler.DevPrincipalUpn);
        body.Principal.TenantId.Should().Be(MockAuthenticationHandler.DevTenantId);
        body.Server.Revision.Should().NotBeNullOrWhiteSpace();
        body.Server.Environment.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AuthenticatedRequest_EchoesReceivedTraceparent()
    {
        using var client = _factory.CreateClient();
        var traceId = ActivityTraceId.CreateRandom().ToHexString();
        var spanId = ActivitySpanId.CreateRandom().ToHexString();
        var traceparent = $"00-{traceId}-{spanId}-01";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.Add("traceparent", traceparent);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<WhoAmIResponse>();
        body.Should().NotBeNull();
        body!.Correlation.ReceivedTraceparent.Should().Be(traceparent);
        body.Correlation.TraceId.Should().Be(traceId);
    }
}

public sealed class WhoAmIAppFactory : WebApplicationFactory<Program>
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

public sealed class UnauthenticatedFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Real-tenant config so the JWT bearer handler is engaged (no mock auth scheme).
        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Production);
        builder.UseSetting("AzureAd:TenantId", "11111111-1111-1111-1111-111111111111");
        builder.UseSetting("AzureAd:Instance", "https://login.microsoftonline.com/");
        builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000000");
        builder.UseSetting("AzureAd:Audience", "api://busterminal-test");
    }
}
