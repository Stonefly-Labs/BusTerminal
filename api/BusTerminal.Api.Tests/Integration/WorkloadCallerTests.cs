using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Identity;
using BusTerminal.Api.Features.RoleProbes;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Integration;

/// <summary>
/// Spec 003 — Phase 5 (US3, SC-003). Exercises the internal-workload-caller
/// path end-to-end via the mock authentication handler:
///
///   - `X-Mock-Caller-Type: Workload` projects an `idtyp=app` claim on the
///     synthesized principal so `PrincipalAccessor` classifies the caller
///     as `CallerType.Workload` (the same classification the deployed
///     `Microsoft.Identity.Web` pipeline produces for real MI-issued tokens).
///   - `X-Mock-Roles: BusTerminal.Reader` populates the `roles` claim so the
///     `CanRead` policy on `/probe/read` evaluates identically to a human caller.
///
/// The success assertions verify (a) the probe returns 200 (the
/// authorization path treats workloads and humans identically — FR-012 no
/// internal-trust bypass — and `BusTerminal.Reader` satisfies `CanRead`)
/// and (b) the resolved `PlatformPrincipal` carries `CallerType=Workload`
/// + the workload OID, which is the same shape the 403 audit-log path
/// emitted by `BusTerminalAuthorizationMiddlewareResultHandler` would
/// surface in App Insights for an unauthorized workload caller (FR-032).
/// </summary>
public sealed class WorkloadCallerTests : IClassFixture<RoleProbeAppFactory>
{
    private readonly RoleProbeAppFactory _factory;

    public WorkloadCallerTests(RoleProbeAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WorkloadCaller_WithReaderRole_CanCallProbeRead()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/read");
        request.Headers.Add(MockAuthenticationHandler.MockCallerTypeHeader, MockAuthenticationHandler.WorkloadCallerType);
        request.Headers.Add(MockAuthenticationHandler.MockRolesHeader, PlatformRoleClaims.Reader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        // Workload OID is propagated end-to-end. Same field the audit-log path
        // would emit as `caller_oid` for a 403 against this same caller.
        doc.RootElement.GetProperty("callerObjectId").GetString()
            .Should().Be(MockAuthenticationHandler.DevWorkloadOid);

        doc.RootElement.GetProperty("callerEffectiveRoles").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain(PlatformRoleClaims.Reader);
    }

    [Fact]
    public async Task WorkloadCaller_WhoAmI_ReturnsWorkloadCallerTypeAndNoHumanFields()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.Add(MockAuthenticationHandler.MockCallerTypeHeader, MockAuthenticationHandler.WorkloadCallerType);
        request.Headers.Add(MockAuthenticationHandler.MockRolesHeader, PlatformRoleClaims.Reader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WhoAmIResponse>();
        body.Should().NotBeNull();

        // The principal-mapping shape verified here is exactly what the
        // 403 audit-log path consumes via `IPlatformPrincipalAccessor` —
        // proving the workload classification is correct here transitively
        // verifies the audit-log entry shape for unauthorized workload callers.
        body!.Principal.CallerType.Should().Be(nameof(CallerType.Workload));
        body.Principal.Oid.Should().Be(MockAuthenticationHandler.DevWorkloadOid);
        body.Principal.TenantId.Should().Be(MockAuthenticationHandler.DevTenantId);
        body.Principal.EffectiveRoles.Should().ContainSingle().Which.Should().Be(PlatformRoleClaims.Reader);

        // Workload tokens do not carry `name` or `preferred_username` — verify
        // those Human-only fields are absent on the projected principal.
        body.Principal.DisplayName.Should().BeNull();
        body.Principal.PreferredUsername.Should().BeNull();
    }

    [Fact]
    public async Task WorkloadCaller_WithoutAuthorizedRole_Returns403_AndProblemDetailsOmitCallerRoles()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/probe/administer");
        request.Headers.Add(MockAuthenticationHandler.MockCallerTypeHeader, MockAuthenticationHandler.WorkloadCallerType);
        request.Headers.Add(MockAuthenticationHandler.MockRolesHeader, PlatformRoleClaims.Reader);
        request.Content = JsonContent.Create(new { message = "hi" });

        var response = await client.SendAsync(request);

        // Workload caller carrying only `Reader` cannot hit `CanAdminister`.
        // Same enforcement path as a human caller with the same roles —
        // FR-012 no internal-trust bypass.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        // FR-033 — caller's effective roles MUST NOT appear in the 403 body.
        doc.RootElement.TryGetProperty("callerEffectiveRoles", out _).Should().BeFalse();
        doc.RootElement.GetProperty("requiredOperationClass").GetString()
            .Should().Be(nameof(OperationClass.Administer));
    }
}
