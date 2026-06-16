using Microsoft.AspNetCore.Authorization;

namespace BusTerminal.Api.Authorization;

/// <summary>
/// Source of truth for the spec-003 role-permission matrix. Mirrored in
/// <c>specs/003-auth-and-identity/contracts/role-permission-matrix.md</c> —
/// if the two disagree, the document is authoritative and this file is a defect.
/// </summary>
public static class RolePolicies
{
    public static IServiceCollection AddBusTerminalRolePolicies(this IServiceCollection services)
    {
        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                OperationClassPolicies.CanRead,
                policy => policy.RequireRole(
                    PlatformRoleClaims.Reader,
                    PlatformRoleClaims.Developer,
                    PlatformRoleClaims.Operator,
                    PlatformRoleClaims.Admin))
            .AddPolicy(
                OperationClassPolicies.CanMutateDomain,
                policy => policy.RequireRole(
                    PlatformRoleClaims.Operator,
                    PlatformRoleClaims.Admin))
            .AddPolicy(
                OperationClassPolicies.CanOperatePlatform,
                policy => policy.RequireRole(
                    PlatformRoleClaims.Operator,
                    PlatformRoleClaims.Admin))
            .AddPolicy(
                OperationClassPolicies.CanAdminister,
                policy => policy.RequireRole(
                    PlatformRoleClaims.Admin))
            .AddPolicy(
                OperationClassPolicies.CanUseDeveloperTooling,
                policy => policy.RequireRole(
                    PlatformRoleClaims.Developer,
                    PlatformRoleClaims.Admin))
            // Spec 008 / research §15. Strictly tighter than spec 006's
            // permissive "any authenticated tenant user may write" stance —
            // namespace mutation requires explicit attestation via the
            // namespace-administrator App Role. Admin is intentionally NOT
            // permitted here: Admin is the platform-administration capability
            // surface and is overpowered for routine namespace onboarding;
            // operators must hold the dedicated role.
            .AddPolicy(
                NamespacePolicies.CanAdministerNamespaces,
                policy => policy.RequireRole(
                    PlatformRoleClaims.NamespaceAdministrator));

        return services;
    }
}

public static class NamespacePolicies
{
    public const string CanAdministerNamespaces = "CanAdministerNamespaces";
}
