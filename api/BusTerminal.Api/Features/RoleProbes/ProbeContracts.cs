using System.ComponentModel.DataAnnotations;
using BusTerminal.Api.Authorization;

namespace BusTerminal.Api.Features.RoleProbes;

public sealed record ProbeResponse(
    OperationClass OperationClass,
    string CallerObjectId,
    IReadOnlyList<string> CallerEffectiveRoles,
    string CorrelationId,
    DateTime ServerTimeUtc);

public sealed record ProbeEchoRequest(
    [property: Required, MinLength(1), MaxLength(256)] string Message);

public sealed record ProbeEchoResponse(
    OperationClass OperationClass,
    string CallerObjectId,
    IReadOnlyList<string> CallerEffectiveRoles,
    string CorrelationId,
    DateTime ServerTimeUtc,
    string Echo);

internal static class ProbeResponseFactory
{
    public static ProbeResponse Build(OperationClass operationClass, IPlatformPrincipalAccessor accessor)
    {
        var principal = accessor.Current;
        var oid = principal?.ObjectId == Guid.Empty || principal is null
            ? string.Empty
            : principal.ObjectId.ToString();
        var roles = principal is null
            ? Array.Empty<string>()
            : principal.EffectiveRoles.Select(r => r.ToClaimValue()).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var correlationId = principal?.CorrelationId ?? string.Empty;
        return new ProbeResponse(
            OperationClass: operationClass,
            CallerObjectId: oid,
            CallerEffectiveRoles: roles,
            CorrelationId: correlationId,
            ServerTimeUtc: DateTime.UtcNow);
    }

    public static ProbeEchoResponse BuildEcho(OperationClass operationClass, IPlatformPrincipalAccessor accessor, string message)
    {
        var basic = Build(operationClass, accessor);
        return new ProbeEchoResponse(
            OperationClass: basic.OperationClass,
            CallerObjectId: basic.CallerObjectId,
            CallerEffectiveRoles: basic.CallerEffectiveRoles,
            CorrelationId: basic.CorrelationId,
            ServerTimeUtc: basic.ServerTimeUtc,
            Echo: message);
    }
}
