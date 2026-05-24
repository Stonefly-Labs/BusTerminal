using BusTerminal.Api.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BusTerminal.Api.Features.RoleProbes;

public static class OperatePlatformProbeEndpoint
{
    public static IEndpointRouteBuilder MapOperatePlatformProbeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/probe/operate", (IPlatformPrincipalAccessor accessor) =>
                TypedResults.Ok(ProbeResponseFactory.Build(OperationClass.OperatePlatform, accessor)))
            .RequireAuthorization(OperationClassPolicies.CanOperatePlatform)
            .WithName("ProbeOperatePlatform")
            .WithTags("role-probes");

        return endpoints;
    }
}
