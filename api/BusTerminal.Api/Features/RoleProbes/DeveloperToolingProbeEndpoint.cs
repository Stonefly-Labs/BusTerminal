using BusTerminal.Api.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BusTerminal.Api.Features.RoleProbes;

public static class DeveloperToolingProbeEndpoint
{
    public static IEndpointRouteBuilder MapDeveloperToolingProbeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/probe/developer", (IPlatformPrincipalAccessor accessor) =>
            {
                // TODO (T088 / US6): augment with IGraphClient.ResolveUserAsync(caller.ObjectId)
                // and include the resolved displayName in the response. The Graph foundation is
                // introduced in Phase 8; until then this probe returns the base ProbeResponse.
                return TypedResults.Ok(ProbeResponseFactory.Build(OperationClass.DeveloperTooling, accessor));
            })
            .RequireAuthorization(OperationClassPolicies.CanUseDeveloperTooling)
            .WithName("ProbeDeveloperTooling")
            .WithTags("role-probes");

        return endpoints;
    }
}
