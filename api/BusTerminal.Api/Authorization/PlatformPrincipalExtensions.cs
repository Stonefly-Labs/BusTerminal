namespace BusTerminal.Api.Authorization;

// Spec 008 / research §15. Discoverability helper for the namespace-administrator
// role — endpoint filters and view-model builders can ask `principal.IsNamespaceAdministrator()`
// instead of reaching into the EffectiveRoles set directly. Mirrors the
// `RolePolicies.CanAdministerNamespaces` authorization policy.
public static class PlatformPrincipalExtensions
{
    public static bool IsNamespaceAdministrator(this PlatformPrincipal? principal)
        => principal is not null
            && principal.EffectiveRoles.Contains(PlatformRole.NamespaceAdministrator);
}
