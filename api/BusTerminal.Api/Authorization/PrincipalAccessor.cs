using System.Diagnostics;
using System.Security.Claims;

namespace BusTerminal.Api.Authorization;

public interface IPlatformPrincipalAccessor
{
    PlatformPrincipal? Current { get; }
}

public sealed class PrincipalAccessor : IPlatformPrincipalAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PrincipalAccessor> _logger;

    public PrincipalAccessor(IHttpContextAccessor httpContextAccessor, ILogger<PrincipalAccessor> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public PlatformPrincipal? Current
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity is null || !user.Identity.IsAuthenticated)
            {
                return null;
            }
            return Build(user);
        }
    }

    private PlatformPrincipal Build(ClaimsPrincipal user)
    {
        var oidString = GetClaim(user, "oid") ?? GetClaim(user, ClaimTypes.NameIdentifier);
        var tidString = GetClaim(user, "tid");
        Guid.TryParse(oidString, out var objectId);
        Guid.TryParse(tidString, out var tenantId);

        var idtyp = GetClaim(user, "idtyp");
        var callerType = string.Equals(idtyp, "app", StringComparison.OrdinalIgnoreCase)
            ? CallerType.Workload
            : CallerType.Human;

        var displayName = callerType == CallerType.Human ? GetClaim(user, "name") : null;
        var username = callerType == CallerType.Human ? GetClaim(user, "preferred_username") : null;

        var effectiveRoles = user.GetEffectiveRoles(_logger);

        var rawClaims = user.Claims
            .GroupBy(c => c.Type, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Value).ToArray(), StringComparer.Ordinal);

        var correlationId = Activity.Current?.TraceId.ToHexString() ?? string.Empty;

        return new PlatformPrincipal(
            ObjectId: objectId,
            TenantId: tenantId,
            CallerType: callerType,
            DisplayName: displayName,
            Username: username,
            EffectiveRoles: effectiveRoles,
            RawClaims: rawClaims,
            CorrelationId: correlationId);
    }

    private static string? GetClaim(ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirst(claimType)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
