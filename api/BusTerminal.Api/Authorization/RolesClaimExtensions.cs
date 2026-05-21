using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Api.Authorization;

public static partial class RolesClaimExtensions
{
    public const string RolesClaimType = "roles";

    public static IReadOnlySet<PlatformRole> GetEffectiveRoles(this ClaimsPrincipal principal, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var roles = new HashSet<PlatformRole>();
        var seenValues = new HashSet<string>(StringComparer.Ordinal);

        foreach (var claim in principal.FindAll(RolesClaimType))
        {
            ProcessClaim(claim.Value, roles, seenValues, logger);
        }

        foreach (var claim in principal.FindAll(ClaimTypes.Role))
        {
            ProcessClaim(claim.Value, roles, seenValues, logger);
        }

        return roles;
    }

    private static void ProcessClaim(
        string value,
        HashSet<PlatformRole> roles,
        HashSet<string> seenValues,
        ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(value) || !seenValues.Add(value))
        {
            return;
        }

        if (PlatformRoleExtensions.TryParseClaimValue(value, out var role))
        {
            roles.Add(role);
        }
        else if (logger is not null)
        {
            LogUnknownRoleRejected(logger, value);
        }
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "unknown role rejected {RoleValue}")]
    private static partial void LogUnknownRoleRejected(ILogger logger, string roleValue);
}
