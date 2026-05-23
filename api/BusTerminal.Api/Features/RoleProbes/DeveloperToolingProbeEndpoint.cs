using BusTerminal.Api.Authorization;
using BusTerminal.Api.Infrastructure.Graph;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Graph.Models.ODataErrors;

namespace BusTerminal.Api.Features.RoleProbes;

public static partial class DeveloperToolingProbeEndpoint
{
    public static IEndpointRouteBuilder MapDeveloperToolingProbeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/probe/developer", async (
                IPlatformPrincipalAccessor accessor,
                IGraphClient graphClient,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var logger = loggerFactory.CreateLogger("BusTerminal.Api.Features.RoleProbes.DeveloperToolingProbeEndpoint");
                var baseResponse = ProbeResponseFactory.Build(OperationClass.DeveloperTooling, accessor);
                var oid = baseResponse.CallerObjectId;

                string? graphResolvedDisplayName = null;
                if (!string.IsNullOrEmpty(oid))
                {
                    try
                    {
                        var resolved = await graphClient.ResolveUserAsync(oid, cancellationToken).ConfigureAwait(false);
                        graphResolvedDisplayName = resolved?.DisplayName;
                    }
                    catch (ODataError ex) when (ex.ResponseStatusCode is 401 or 403)
                    {
                        // FR-024 graceful degradation: if admin consent for User.Read.All
                        // hasn't been granted yet (or the API's MI lacks the role
                        // assignment), surface `graphResolvedDisplayName: null` instead
                        // of failing the probe. The whole point of this probe is to
                        // smoke the Graph foundation — operators learn from the warning
                        // that consent is the next required step.
                        LogGraphResolveFailedDueToConsent(logger, ex.ResponseStatusCode, oid);
                    }
                }

                return TypedResults.Ok(new DeveloperToolingProbeResponse(
                    OperationClass: baseResponse.OperationClass,
                    CallerObjectId: baseResponse.CallerObjectId,
                    CallerEffectiveRoles: baseResponse.CallerEffectiveRoles,
                    CorrelationId: baseResponse.CorrelationId,
                    ServerTimeUtc: baseResponse.ServerTimeUtc,
                    GraphResolvedDisplayName: graphResolvedDisplayName));
            })
            .RequireAuthorization(OperationClassPolicies.CanUseDeveloperTooling)
            .WithName("ProbeDeveloperTooling")
            .WithTags("role-probes");

        return endpoints;
    }

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "Microsoft Graph self-resolve failed with status {GraphStatusCode}; admin consent for User.Read.All may not yet be granted. CallerOid={CallerOid}")]
    static partial void LogGraphResolveFailedDueToConsent(ILogger logger, int graphStatusCode, string callerOid);
}

public sealed record DeveloperToolingProbeResponse(
    OperationClass OperationClass,
    string CallerObjectId,
    IReadOnlyList<string> CallerEffectiveRoles,
    string CorrelationId,
    DateTime ServerTimeUtc,
    string? GraphResolvedDisplayName);
