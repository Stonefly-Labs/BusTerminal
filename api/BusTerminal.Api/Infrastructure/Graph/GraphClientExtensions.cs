namespace BusTerminal.Api.Infrastructure.Graph;

public static class GraphClientExtensions
{
    public static IServiceCollection AddBusTerminalGraphClient(this IServiceCollection services)
    {
        // Singleton — GraphServiceClient is thread-safe and intended to be
        // reused. Holding the same instance across requests keeps the
        // underlying TokenCredential's in-memory token cache warm; per-request
        // construction would re-probe the DefaultAzureCredential chain and
        // re-acquire from IMDS on every call.
        services.AddSingleton<IGraphClient, GraphClient>();
        return services;
    }
}
