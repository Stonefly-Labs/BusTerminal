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
        if (_environment.IsDevelopment())
        {
            return new DefaultAzureCredential();
        }

        var options = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(userAssignedClientId))
        {
            options.ManagedIdentityClientId = userAssignedClientId;
        }
        return new DefaultAzureCredential(options);
    }
}
