using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.RoleProbes;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BusTerminal.Api.Tests.Integration;

/// <summary>
/// Full role-permission matrix coverage for the five probe endpoints (FR-009c).
/// 5 probes × 4 roles + 1 no-role variant = 25 authenticated cases. Plus a 401
/// per probe = 5 unauthenticated cases. 30 cases total.
/// </summary>
public sealed class RoleProbeEndpointTests : IClassFixture<RoleProbeAppFactory>, IClassFixture<UnauthenticatedFactory>
{
    private readonly RoleProbeAppFactory _factory;
    private readonly UnauthenticatedFactory _unauthenticatedFactory;

    public RoleProbeEndpointTests(RoleProbeAppFactory factory, UnauthenticatedFactory unauthenticatedFactory)
    {
        _factory = factory;
        _unauthenticatedFactory = unauthenticatedFactory;
    }

    private static readonly IReadOnlyDictionary<string, HashSet<string>> ExpectedAuthorizedRoles =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["/probe/read"] =
            [
                PlatformRoleClaims.Reader,
                PlatformRoleClaims.Developer,
                PlatformRoleClaims.Operator,
                PlatformRoleClaims.Admin,
            ],
            ["/probe/mutate-domain"] =
            [
                PlatformRoleClaims.Operator,
                PlatformRoleClaims.Admin,
            ],
            ["/probe/operate"] =
            [
                PlatformRoleClaims.Operator,
                PlatformRoleClaims.Admin,
            ],
            ["/probe/administer"] =
            [
                PlatformRoleClaims.Admin,
            ],
            ["/probe/developer"] =
            [
                PlatformRoleClaims.Developer,
                PlatformRoleClaims.Admin,
            ],
        };

    public static IEnumerable<object[]> AllRoleCombos()
    {
        var probes = new[]
        {
            ("/probe/read", HttpMethod.Get, false),
            ("/probe/mutate-domain", HttpMethod.Post, true),
            ("/probe/operate", HttpMethod.Post, false),
            ("/probe/administer", HttpMethod.Post, true),
            ("/probe/developer", HttpMethod.Get, false),
        };
        var roles = new[]
        {
            PlatformRoleClaims.Admin,
            PlatformRoleClaims.Operator,
            PlatformRoleClaims.Reader,
            PlatformRoleClaims.Developer,
            string.Empty, // no-role
        };
        foreach (var (path, method, needsBody) in probes)
        {
            foreach (var role in roles)
            {
                yield return new object[] { path, method.Method, needsBody, role };
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllRoleCombos))]
    public async Task ProbeEndpoint_AuthorizationOutcomeMatchesMatrix(string path, string methodName, bool needsBody, string role)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(methodName), path);
        if (!string.IsNullOrEmpty(role))
        {
            request.Headers.Add(MockAuthenticationHandler.MockRolesHeader, role);
        }
        if (needsBody)
        {
            request.Content = JsonContent.Create(new { message = "hello" });
        }

        var response = await client.SendAsync(request);

        var expected = ExpectedAuthorizedRoles[path];
        if (!string.IsNullOrEmpty(role) && expected.Contains(role))
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"role {role} is authorized for {path}");
            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            doc.RootElement.GetProperty("callerObjectId").GetString()
                .Should().NotBeNullOrEmpty();
            doc.RootElement.GetProperty("callerEffectiveRoles").EnumerateArray()
                .Select(e => e.GetString()).Should().Contain(role);
            if (needsBody)
            {
                doc.RootElement.GetProperty("echo").GetString().Should().Be("hello");
            }
        }
        else
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"role {role} is NOT authorized for {path}");
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            doc.RootElement.GetProperty("status").GetInt32().Should().Be(403);
            doc.RootElement.GetProperty("requiredOperationClass").GetString()
                .Should().NotBeNullOrEmpty();
            var requiredRoles = doc.RootElement.GetProperty("requiredRoles").EnumerateArray()
                .Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal);
            requiredRoles.Should().BeEquivalentTo(expected);
            // FR-033: caller's effective roles must NOT be echoed in the problem-details body.
            doc.RootElement.TryGetProperty("callerEffectiveRoles", out _).Should().BeFalse();
        }
    }

    [Theory]
    [InlineData("/probe/read", "GET", false)]
    [InlineData("/probe/mutate-domain", "POST", true)]
    [InlineData("/probe/operate", "POST", false)]
    [InlineData("/probe/administer", "POST", true)]
    [InlineData("/probe/developer", "GET", false)]
    public async Task ProbeEndpoint_ReturnsUnauthorized_ForMissingToken(string path, string methodName, bool needsBody)
    {
        using var client = _unauthenticatedFactory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(methodName), path);
        if (needsBody)
        {
            request.Content = JsonContent.Create(new { message = "hello" });
        }

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().NotBeEmpty();
    }
}

public sealed class RoleProbeAppFactory : WebApplicationFactory<Program>
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
