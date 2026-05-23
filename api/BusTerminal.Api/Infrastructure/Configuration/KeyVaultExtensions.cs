using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using BusTerminal.Api.Infrastructure.Credentials;

namespace BusTerminal.Api.Infrastructure.Configuration;

public static class KeyVaultExtensions
{
    public const string KeyVaultUriEnvironmentVariable = "AZURE_KEY_VAULT_URI";

    // FR-019 remediation message for local developers whose `az login` has lapsed
    // or who never signed in. The tenant id is the BusTerminal dev tenant.
    internal const string DevCredentialRemediationMessage =
        "Azure credentials unavailable. Run: az login --tenant 596c1564-6e95-4c35-a80b-2dbe45a162f3";

    private const string KeyVaultScope = "https://vault.azure.net/.default";

    public static IConfigurationBuilder AddBusTerminalKeyVault(
        this IConfigurationBuilder configuration,
        IHostEnvironment environment,
        IAzureCredentialFactory credentialFactory)
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

        var credential = credentialFactory.CreateCredential();

        if (environment.IsDevelopment())
        {
            try
            {
                credential.GetToken(
                    new TokenRequestContext(new[] { KeyVaultScope }),
                    CancellationToken.None);
            }
            catch (CredentialUnavailableException ex)
            {
                throw new InvalidOperationException(DevCredentialRemediationMessage, ex);
            }
        }

        configuration.AddAzureKeyVault(parsedUri, credential, new KeyVaultSecretManager());

        return configuration;
    }
}
