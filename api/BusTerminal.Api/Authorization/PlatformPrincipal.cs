namespace BusTerminal.Api.Authorization;

public enum CallerType
{
    Human,
    Workload,
}

public sealed record PlatformPrincipal(
    Guid ObjectId,
    Guid TenantId,
    CallerType CallerType,
    string? DisplayName,
    string? Username,
    IReadOnlySet<PlatformRole> EffectiveRoles,
    IReadOnlyDictionary<string, string[]> RawClaims,
    string CorrelationId);
