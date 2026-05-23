using Azure.Core;
using Azure.Identity;

namespace BusTerminal.Api.Infrastructure.Credentials;

public interface IAzureCredentialFactory
{
    TokenCredential CreateCredential(string? userAssignedClientId = null);
}

public sealed class AzureCredentialFactory : IAzureCredentialFactory
{
    private readonly IHostEnvironment _environment;

    public AzureCredentialFactory(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public TokenCredential CreateCredential(string? userAssignedClientId = null)
    {
        var options = BuildOptions(_environment, userAssignedClientId);
        return options is null
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(options);
    }

    internal static DefaultAzureCredentialOptions? BuildOptions(
        IHostEnvironment environment,
        string? userAssignedClientId)
    {
        if (environment.IsDevelopment())
        {
            return null;
        }

        var options = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(userAssignedClientId))
        {
            options.ManagedIdentityClientId = userAssignedClientId;
        }
        return options;
    }
}
