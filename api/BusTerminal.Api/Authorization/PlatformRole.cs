using System.ComponentModel;

namespace BusTerminal.Api.Authorization;

public enum PlatformRole
{
    [Description(PlatformRoleClaims.Admin)]
    Admin,

    [Description(PlatformRoleClaims.Operator)]
    Operator,

    [Description(PlatformRoleClaims.Reader)]
    Reader,

    [Description(PlatformRoleClaims.Developer)]
    Developer,

    [Description(PlatformRoleClaims.NamespaceAdministrator)]
    NamespaceAdministrator,
}

public static class PlatformRoleClaims
{
    public const string Admin = "BusTerminal.Admin";
    public const string Operator = "BusTerminal.Operator";
    public const string Reader = "BusTerminal.Reader";
    public const string Developer = "BusTerminal.Developer";
    public const string NamespaceAdministrator = "BusTerminal.NamespaceAdministrator";
}

public static class PlatformRoleExtensions
{
    public static string ToClaimValue(this PlatformRole role) => role switch
    {
        PlatformRole.Admin => PlatformRoleClaims.Admin,
        PlatformRole.Operator => PlatformRoleClaims.Operator,
        PlatformRole.Reader => PlatformRoleClaims.Reader,
        PlatformRole.Developer => PlatformRoleClaims.Developer,
        PlatformRole.NamespaceAdministrator => PlatformRoleClaims.NamespaceAdministrator,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown PlatformRole."),
    };

    public static bool TryParseClaimValue(string value, out PlatformRole role)
    {
        switch (value)
        {
            case PlatformRoleClaims.Admin:
                role = PlatformRole.Admin;
                return true;
            case PlatformRoleClaims.Operator:
                role = PlatformRole.Operator;
                return true;
            case PlatformRoleClaims.Reader:
                role = PlatformRole.Reader;
                return true;
            case PlatformRoleClaims.Developer:
                role = PlatformRole.Developer;
                return true;
            case PlatformRoleClaims.NamespaceAdministrator:
                role = PlatformRole.NamespaceAdministrator;
                return true;
            default:
                role = default;
                return false;
        }
    }
}
