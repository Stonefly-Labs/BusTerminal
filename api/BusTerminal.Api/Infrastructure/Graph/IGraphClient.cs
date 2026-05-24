namespace BusTerminal.Api.Infrastructure.Graph;

// Delegated Graph flows (FR-025) can be added in a later slice by injecting a user-context TokenCredential via the AzureCredentialFactory — no breaking change to this interface required.
public interface IGraphClient
{
    Task<GraphUser?> ResolveUserAsync(string objectId, CancellationToken cancellationToken);
}

public sealed record GraphUser(
    string ObjectId,
    string? DisplayName,
    string? UserPrincipalName,
    string? Mail);
