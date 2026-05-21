using BusTerminal.Api.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BusTerminal.Api.Features.RoleProbes;

public static class AdministerProbeEndpoint
{
    public static IEndpointRouteBuilder MapAdministerProbeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/probe/administer", (ProbeEchoRequest body, IPlatformPrincipalAccessor accessor) =>
                TypedResults.Ok(ProbeResponseFactory.BuildEcho(OperationClass.Administer, accessor, body.Message)))
            .RequireAuthorization(OperationClassPolicies.CanAdminister)
            .WithName("ProbeAdminister")
            .WithTags("role-probes");

        return endpoints;
    }
}
