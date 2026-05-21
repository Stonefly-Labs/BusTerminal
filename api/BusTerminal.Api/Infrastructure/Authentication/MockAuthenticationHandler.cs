using System.Security.Claims;
using System.Text.Encodings.Web;
using BusTerminal.Api.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Authentication;

public sealed class MockAuthenticationOptions : AuthenticationSchemeOptions
{
}

public sealed class MockAuthenticationHandler : AuthenticationHandler<MockAuthenticationOptions>
{
    public const string SchemeName = "MockAuth";
    public const string DevPrincipalOid = "00000000-0000-0000-0000-000000000001";
    public const string DevPrincipalDisplayName = "Dev User";
    public const string DevPrincipalUpn = "dev.user@busterminal.local";
    public const string DevTenantId = "00000000-0000-0000-0000-000000000002";
    public const string MockRolesHeader = "X-Mock-Roles";

    private readonly IHostEnvironment _environment;

    public MockAuthenticationHandler(
        IOptionsMonitor<MockAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _environment = environment;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "MockAuthenticationHandler is gated to the Development environment and must never be reachable in Production.");
        }

        var claims = new List<Claim>
        {
            new("oid", DevPrincipalOid),
            new(ClaimTypes.NameIdentifier, DevPrincipalOid),
            new(ClaimTypes.Name, DevPrincipalDisplayName),
            new("name", DevPrincipalDisplayName),
            new("preferred_username", DevPrincipalUpn),
            new("tid", DevTenantId),
        };

        foreach (var role in ParseRolesHeader(Request.Headers[MockRolesHeader]))
        {
            var value = role.ToClaimValue();
            claims.Add(new Claim(ClaimTypes.Role, value));
            claims.Add(new Claim(RolesClaimExtensions.RolesClaimType, value));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, "name", ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"{SchemeName} realm=\"busterminal\"";
        return Task.CompletedTask;
    }

    private static IEnumerable<PlatformRole> ParseRolesHeader(Microsoft.Extensions.Primitives.StringValues header)
    {
        if (header.Count == 0)
        {
            yield break;
        }

        var seen = new HashSet<PlatformRole>();
        foreach (var raw in header)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (PlatformRoleExtensions.TryParseClaimValue(part, out var role) && seen.Add(role))
                {
                    yield return role;
                }
            }
        }
    }
}
