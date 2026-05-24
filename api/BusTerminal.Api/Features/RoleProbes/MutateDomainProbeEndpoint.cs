using BusTerminal.Api.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BusTerminal.Api.Features.RoleProbes;

public static class MutateDomainProbeEndpoint
{
    public static IEndpointRouteBuilder MapMutateDomainProbeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/probe/mutate-domain", (ProbeEchoRequest body, IPlatformPrincipalAccessor accessor) =>
                TypedResults.Ok(ProbeResponseFactory.BuildEcho(OperationClass.MutateDomain, accessor, body.Message)))
            .RequireAuthorization(OperationClassPolicies.CanMutateDomain)
            .WithName("ProbeMutateDomain")
            .WithTags("role-probes");

        return endpoints;
    }
}
