using System.Security.Claims;
using BusTerminal.Api.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BusTerminal.Api.Tests.Unit.Authorization;

/// <summary>
/// Exhaustive matrix coverage for the five operation-class policies registered
/// by <see cref="RolePolicies.AddBusTerminalRolePolicies"/>. Asserts:
///
/// - 4 roles × 5 classes = 20 single-role cases that match the matrix exactly.
/// - 1 no-role case that fails every policy.
///
/// Authoritative reference: contracts/role-permission-matrix.md.
/// </summary>
public sealed class RolePoliciesTests
{
    private readonly IAuthorizationService _authorizationService;

    public RolePoliciesTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddBusTerminalRolePolicies();
        var provider = services.BuildServiceProvider();
        _authorizationService = provider.GetRequiredService<IAuthorizationService>();
    }

    public static IEnumerable<object[]> SingleRoleCases()
    {
        var matrix = new Dictionary<string, string[]>
        {
            [OperationClassPolicies.CanRead] =
            [
                PlatformRoleClaims.Reader,
                PlatformRoleClaims.Developer,
                PlatformRoleClaims.Operator,
                PlatformRoleClaims.Admin,
            ],
            [OperationClassPolicies.CanMutateDomain] =
            [
                PlatformRoleClaims.Operator,
                PlatformRoleClaims.Admin,
            ],
            [OperationClassPolicies.CanOperatePlatform] =
            [
                PlatformRoleClaims.Operator,
                PlatformRoleClaims.Admin,
            ],
            [OperationClassPolicies.CanAdminister] =
            [
                PlatformRoleClaims.Admin,
            ],
            [OperationClassPolicies.CanUseDeveloperTooling] =
            [
                PlatformRoleClaims.Developer,
                PlatformRoleClaims.Admin,
            ],
        };

        foreach (var (policy, allowedRoles) in matrix)
        {
            foreach (var role in new[]
            {
                PlatformRoleClaims.Admin,
                PlatformRoleClaims.Operator,
                PlatformRoleClaims.Reader,
                PlatformRoleClaims.Developer,
            })
            {
                yield return new object[] { policy, role, allowedRoles.Contains(role) };
            }
        }
    }

    [Theory]
    [MemberData(nameof(SingleRoleCases))]
    public async Task SingleRole_AuthorizationOutcomeMatchesMatrix(string policy, string role, bool expectedSuccess)
    {
        var principal = BuildPrincipal(role);

        var result = await _authorizationService.AuthorizeAsync(principal, resource: null, policy);

        result.Succeeded.Should().Be(expectedSuccess, $"policy={policy} role={role}");
    }

    [Theory]
    [InlineData(OperationClassPolicies.CanRead)]
    [InlineData(OperationClassPolicies.CanMutateDomain)]
    [InlineData(OperationClassPolicies.CanOperatePlatform)]
    [InlineData(OperationClassPolicies.CanAdminister)]
    [InlineData(OperationClassPolicies.CanUseDeveloperTooling)]
    public async Task NoRole_AuthorizationFails(string policy)
    {
        var principal = BuildPrincipal();

        var result = await _authorizationService.AuthorizeAsync(principal, resource: null, policy);

        result.Succeeded.Should().BeFalse($"a roleless caller must fail every operation-class policy (policy={policy})");
    }

    private static ClaimsPrincipal BuildPrincipal(params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r));
        var identity = new ClaimsIdentity(claims, authenticationType: "Test", nameType: "name", roleType: ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
