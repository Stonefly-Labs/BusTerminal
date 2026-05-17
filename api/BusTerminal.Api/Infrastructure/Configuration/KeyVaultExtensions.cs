using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

namespace BusTerminal.Api.Infrastructure.Configuration;

public static class KeyVaultExtensions
{
    public const string KeyVaultUriEnvironmentVariable = "AZURE_KEY_VAULT_URI";

    public static IConfigurationBuilder AddBusTerminalKeyVault(
        this IConfigurationBuilder configuration,
        IHostEnvironment environment)
    {
        var vaultUri = Environment.GetEnvironmentVariable(KeyVaultUriEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(vaultUri))
        {
            return configuration;
        }

        if (!Uri.TryCreate(vaultUri, UriKind.Absolute, out var parsedUri))
        {
            throw new InvalidOperationException(
                $"{KeyVaultUriEnvironmentVariable} is set to '{vaultUri}' which is not a valid absolute URI.");
        }

        var credential = new DefaultAzureCredential(includeInteractiveCredentials: environment.IsDevelopment());

        configuration.AddAzureKeyVault(parsedUri, credential, new KeyVaultSecretManager());

        return configuration;
    }
}
