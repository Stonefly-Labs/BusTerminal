using BusTerminal.Api.Authorization;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Unit.Authorization;

// Spec 008 / T018. Coverage matrix for IsNamespaceAdministrator():
//   - principal carrying the role → true
//   - principal carrying only spec-003 roles → false
//   - null principal handling (defensive).
public sealed class PlatformPrincipalExtensionsTests
{
    [Fact]
    public void IsNamespaceAdministrator_PrincipalCarryingTheRole_ReturnsTrue()
    {
        var principal = NewPrincipal(PlatformRole.NamespaceAdministrator);

        principal.IsNamespaceAdministrator().Should().BeTrue();
    }

    [Fact]
    public void IsNamespaceAdministrator_PrincipalCarryingTheRoleAlongsideOthers_ReturnsTrue()
    {
        var principal = NewPrincipal(
            PlatformRole.Admin,
            PlatformRole.Operator,
            PlatformRole.NamespaceAdministrator);

        principal.IsNamespaceAdministrator().Should().BeTrue();
    }

    [Theory]
    [InlineData(PlatformRole.Admin)]
    [InlineData(PlatformRole.Operator)]
    [InlineData(PlatformRole.Reader)]
    [InlineData(PlatformRole.Developer)]
    public void IsNamespaceAdministrator_PrincipalWithOnlySpec003Roles_ReturnsFalse(PlatformRole onlyRole)
    {
        var principal = NewPrincipal(onlyRole);

        principal.IsNamespaceAdministrator().Should().BeFalse();
    }

    [Fact]
    public void IsNamespaceAdministrator_PrincipalWithNoRoles_ReturnsFalse()
    {
        var principal = NewPrincipal();

        principal.IsNamespaceAdministrator().Should().BeFalse();
    }

    [Fact]
    public void IsNamespaceAdministrator_NullPrincipal_ReturnsFalse()
    {
        PlatformPrincipal? principal = null;

        principal.IsNamespaceAdministrator().Should().BeFalse();
    }

    private static PlatformPrincipal NewPrincipal(params PlatformRole[] roles)
    {
        return new PlatformPrincipal(
            ObjectId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            CallerType: CallerType.Human,
            DisplayName: "Test User",
            Username: "test@example.com",
            EffectiveRoles: new HashSet<PlatformRole>(roles),
            RawClaims: new Dictionary<string, string[]>(),
            CorrelationId: Guid.NewGuid().ToString("D"));
    }
}
