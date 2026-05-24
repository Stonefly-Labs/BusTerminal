using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Api.Authorization;

/// <summary>
/// Custom <see cref="IAuthorizationMiddlewareResultHandler"/> that converts 403
/// results from BusTerminal operation-class policies into an RFC 7807
/// <c>application/problem+json</c> body matching the AuthorizationProblem
/// schema in <c>contracts/role-probes.openapi.yaml</c>.
///
/// Falls back to the framework default for every other authorization outcome
/// (challenge, success, and forbid results that did not originate from a
/// BusTerminal operation-class policy).
///
/// Also emits FR-032 structured logging on every 403: caller oid, caller
/// effective roles, required operation class, required roles, correlation id.
/// Token contents are NEVER logged (FR-033).
/// </summary>
public sealed partial class BusTerminalAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly ILogger<BusTerminalAuthorizationMiddlewareResultHandler> _logger;

    public BusTerminalAuthorizationMiddlewareResultHandler(
        ILogger<BusTerminalAuthorizationMiddlewareResultHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var metadata = AuthorizationProblemFactory.ResolveMetadata(context);
            if (metadata is not null)
            {
                LogForbidden(context, metadata);
                await AuthorizationProblemFactory.WriteAsync(context, metadata);
                return;
            }
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private void LogForbidden(HttpContext context, AuthorizationPolicyMetadata metadata)
    {
        var principalAccessor = context.RequestServices.GetService<IPlatformPrincipalAccessor>();
        var principal = principalAccessor?.Current;
        var callerOid = principal?.ObjectId == Guid.Empty || principal is null
            ? string.Empty
            : principal.ObjectId.ToString();
        var callerRoles = principal is null
            ? Array.Empty<string>()
            : principal.EffectiveRoles
                .Select(r => r.ToClaimValue())
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();
        var correlationId = principal?.CorrelationId
            ?? Activity.Current?.TraceId.ToHexString()
            ?? string.Empty;

        LogAuthorizationForbidden(
            _logger,
            callerOid,
            string.Join(',', callerRoles),
            metadata.OperationClass.ToString(),
            string.Join(',', metadata.RequiredRoles),
            correlationId);
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "authorization forbidden caller_oid={CallerOid} caller_effective_roles={CallerEffectiveRoles} required_operation_class={RequiredOperationClass} required_roles={RequiredRoles} correlation_id={CorrelationId}")]
    private static partial void LogAuthorizationForbidden(
        ILogger logger,
        string callerOid,
        string callerEffectiveRoles,
        string requiredOperationClass,
        string requiredRoles,
        string correlationId);
}
