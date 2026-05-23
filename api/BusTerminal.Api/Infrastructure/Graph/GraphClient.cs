using BusTerminal.Api.Infrastructure.Credentials;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace BusTerminal.Api.Infrastructure.Graph;

public sealed class GraphClient : IGraphClient
{
    private readonly Func<string, CancellationToken, Task<User?>> _fetchUser;

    public GraphClient(IAzureCredentialFactory credentialFactory)
    {
        ArgumentNullException.ThrowIfNull(credentialFactory);
        var credential = credentialFactory.CreateCredential();
        var graph = new GraphServiceClient(credential);
        _fetchUser = (objectId, ct) => graph.Users[objectId].GetAsync(cancellationToken: ct);
    }

    internal GraphClient(Func<string, CancellationToken, Task<User?>> fetchUser)
    {
        _fetchUser = fetchUser ?? throw new ArgumentNullException(nameof(fetchUser));
    }

    public async Task<GraphUser?> ResolveUserAsync(string objectId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        try
        {
            var user = await _fetchUser(objectId, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                return null;
            }

            return new GraphUser(
                ObjectId: user.Id ?? objectId,
                DisplayName: user.DisplayName,
                UserPrincipalName: user.UserPrincipalName,
                Mail: user.Mail);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            return null;
        }
    }
}
