using Azure.Core;
using Azure.Identity;

namespace BusTerminal.Api.Infrastructure.Credentials;

public interface IAzureCredentialFactory
{
    TokenCredential CreateCredential(string? userAssignedClientId = null);
}

// Constitution §IV Security by Default + spec-006 plan.md §Identity. Single
// credential-acquisition path so token caching, retry policy, and identity
// selection are uniform across every Azure SDK client.
//
// Behaviour:
//   - **Development** → DefaultAzureCredential (no options) so the local
//     developer's az/VSCode/VS identity resolves through the full chain.
//   - **Non-Development + userAssignedClientId set** → ManagedIdentityCredential
//     pinned to that client id. Skips the DefaultAzureCredential chain probe
//     (env → workload identity → MI → SharedTokenCache → VS → VSC → CLI → …) —
//     in production the workload UAMI is the only credible source, so probing
//     the chain wastes 30-100ms on the first GetToken per credential instance
//     AND leaves the door open to picking up a system-assigned MI if one ever
//     gets attached.
//   - **Non-Development + no userAssignedClientId** → DefaultAzureCredential.
//     Safer fall-through when the caller didn't plumb the UAMI client id —
//     surfaces in logs as a chain probe rather than a wrong-identity bug.
//
// Token caching is in-memory and per-instance. Each consumer holds a long-
// lived singleton (CosmosClient, SearchClient, GraphServiceClient, KV config
// provider) so the cache stays warm for the process lifetime.
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

        if (!string.IsNullOrWhiteSpace(userAssignedClientId))
        {
            return new ManagedIdentityCredential(
                ManagedIdentityId.FromUserAssignedClientId(userAssignedClientId));
        }

        // Non-Development with no UAMI client id wired. Fall back to the
        // chain — system-assigned MI or any other credential the SDK can find.
        return new DefaultAzureCredential();
    }
}
