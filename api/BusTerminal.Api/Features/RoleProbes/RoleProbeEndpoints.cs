namespace BusTerminal.Api.Features.RoleProbes;

public static class RoleProbeEndpoints
{
    public static IEndpointRouteBuilder MapRoleProbeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapReadProbeEndpoint()
            .MapMutateDomainProbeEndpoint()
            .MapOperatePlatformProbeEndpoint()
            .MapAdministerProbeEndpoint()
            .MapDeveloperToolingProbeEndpoint();

        return endpoints;
    }
}
