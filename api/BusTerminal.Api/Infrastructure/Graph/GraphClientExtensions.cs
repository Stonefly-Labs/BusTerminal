namespace BusTerminal.Api.Infrastructure.Graph;

public static class GraphClientExtensions
{
    public static IServiceCollection AddBusTerminalGraphClient(this IServiceCollection services)
    {
        services.AddScoped<IGraphClient, GraphClient>();
        return services;
    }
}
