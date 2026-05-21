using BusTerminal.Api.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BusTerminal.Api.Features.RoleProbes;

public static class ReadProbeEndpoint
{
    public static IEndpointRouteBuilder MapReadProbeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/probe/read", (IPlatformPrincipalAccessor accessor) =>
                TypedResults.Ok(ProbeResponseFactory.Build(OperationClass.Read, accessor)))
            .RequireAuthorization(OperationClassPolicies.CanRead)
            .WithName("ProbeRead")
            .WithTags("role-probes");

        return endpoints;
    }
}
