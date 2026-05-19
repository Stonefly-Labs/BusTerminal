using System.Security.Claims;
using System.Text.Encodings.Web;
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

        var claims = new[]
        {
            new Claim("oid", DevPrincipalOid),
            new Claim(ClaimTypes.NameIdentifier, DevPrincipalOid),
            new Claim(ClaimTypes.Name, DevPrincipalDisplayName),
            new Claim("name", DevPrincipalDisplayName),
            new Claim("preferred_username", DevPrincipalUpn),
            new Claim("tid", DevTenantId),
        };

        var identity = new ClaimsIdentity(claims, SchemeName, "name", string.Empty);
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
}
