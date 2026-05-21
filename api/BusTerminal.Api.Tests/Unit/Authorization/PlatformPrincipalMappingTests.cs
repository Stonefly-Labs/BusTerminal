using System.Security.Claims;
using BusTerminal.Api.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BusTerminal.Api.Tests.Unit.Authorization;

/// <summary>
/// Claims → <see cref="PlatformPrincipal"/> projection.
/// Covers: human token shape, app-only token shape, missing optional claims,
/// unknown role values dropped, oid and tid propagation.
/// </summary>
public sealed class PlatformPrincipalMappingTests
{
    private static readonly Guid HumanOid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid WorkloadOid = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void HumanToken_ProjectsAllHumanFields()
    {
        var principal = BuildPrincipal(
            ("oid", HumanOid.ToString()),
            ("tid", TenantId.ToString()),
            ("name", "Chris House"),
            ("preferred_username", "chris@busterminal.example"),
            ("roles", PlatformRoleClaims.Operator),
            ("roles", PlatformRoleClaims.Reader));

        var resolved = BuildAccessor(principal).Current;

        resolved.Should().NotBeNull();
        resolved!.ObjectId.Should().Be(HumanOid);
        resolved.TenantId.Should().Be(TenantId);
        resolved.CallerType.Should().Be(CallerType.Human);
        resolved.DisplayName.Should().Be("Chris House");
        resolved.Username.Should().Be("chris@busterminal.example");
        resolved.EffectiveRoles.Should().BeEquivalentTo(new[] { PlatformRole.Operator, PlatformRole.Reader });
    }

    [Fact]
    public void AppOnlyToken_ProjectsWorkloadCaller_WithoutHumanFields()
    {
        var principal = BuildPrincipal(
            ("oid", WorkloadOid.ToString()),
            ("tid", TenantId.ToString()),
            ("idtyp", "app"),
            ("roles", PlatformRoleClaims.Reader));

        var resolved = BuildAccessor(principal).Current;

        resolved.Should().NotBeNull();
        resolved!.CallerType.Should().Be(CallerType.Workload);
        resolved.ObjectId.Should().Be(WorkloadOid);
        resolved.DisplayName.Should().BeNull();
        resolved.Username.Should().BeNull();
        resolved.EffectiveRoles.Should().BeEquivalentTo(new[] { PlatformRole.Reader });
    }

    [Fact]
    public void MissingOptionalClaims_DoesNotThrow_AndProducesEmptyDisplayFields()
    {
        var principal = BuildPrincipal(
            ("oid", HumanOid.ToString()),
            ("tid", TenantId.ToString()));

        var resolved = BuildAccessor(principal).Current;

        resolved.Should().NotBeNull();
        resolved!.DisplayName.Should().BeNull();
        resolved.Username.Should().BeNull();
        resolved.EffectiveRoles.Should().BeEmpty();
    }

    [Fact]
    public void UnknownRoleValues_AreDroppedSilently()
    {
        var principal = BuildPrincipal(
            ("oid", HumanOid.ToString()),
            ("tid", TenantId.ToString()),
            ("roles", PlatformRoleClaims.Reader),
            ("roles", "BusTerminal.SuperAdmin"),
            ("roles", ""),
            ("roles", "Random.Role"));

        var resolved = BuildAccessor(principal).Current;

        resolved.Should().NotBeNull();
        resolved!.EffectiveRoles.Should().BeEquivalentTo(new[] { PlatformRole.Reader });
    }

    [Fact]
    public void RolesClaim_AcceptedFromBothShortAndClaimTypesRoleNames()
    {
        var principal = BuildPrincipal(
            ("oid", HumanOid.ToString()),
            ("tid", TenantId.ToString()));
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(ClaimTypes.Role, PlatformRoleClaims.Admin));
        identity.AddClaim(new Claim(RolesClaimExtensions.RolesClaimType, PlatformRoleClaims.Developer));

        var resolved = BuildAccessor(principal).Current;

        resolved.Should().NotBeNull();
        resolved!.EffectiveRoles.Should().BeEquivalentTo(new[] { PlatformRole.Admin, PlatformRole.Developer });
    }

    [Fact]
    public void UnauthenticatedUser_ReturnsNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var resolved = BuildAccessor(principal).Current;

        resolved.Should().BeNull();
    }

    private static ClaimsPrincipal BuildPrincipal(params (string Type, string Value)[] claims)
    {
        var mapped = claims.Select(c => new Claim(c.Type, c.Value)).ToList();
        var identity = new ClaimsIdentity(mapped, authenticationType: "Test", nameType: "name", roleType: ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    private static PrincipalAccessor BuildAccessor(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new PrincipalAccessor(accessor, NullLogger<PrincipalAccessor>.Instance);
    }
}
