using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Infrastructure.Credentials;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace BusTerminal.Api.Infrastructure.Graph;

// Spec 008 / research §2 + contracts/namespace-onboarding-api.yaml#/_picker.
// Wraps the existing Graph SDK v5 surface — users via graph.Users.GetAsync,
// groups via graph.Groups.GetAsync. Results merged + display-name-ascending +
// capped at `top`. Empty query → empty result.
//
// The Graph SDK v5 wraps the transport via IRequestAdapter; for the same
// reasons documented on GraphClient (Kiota-generated surface is expensive to
// stub adapter-side), GraphPrincipalPicker exposes delegate seams the unit
// tests inject directly. Production wiring (the public ctor) builds the
// GraphServiceClient via the existing AzureCredentialFactory pattern.
//
// PII boundary: results carry display name + UPN + mail (necessary for the
// picker UX); the picker proxy endpoint never logs these values.
public sealed partial class GraphPrincipalPicker : IGraphPrincipalPicker
{
    private readonly Func<string, int, CancellationToken, Task<UserCollectionResponse?>> _fetchUsers;
    private readonly Func<string, int, CancellationToken, Task<GroupCollectionResponse?>> _fetchGroups;
    private readonly ILogger<GraphPrincipalPicker> _logger;

    public GraphPrincipalPicker(
        IAzureCredentialFactory credentialFactory,
        IConfiguration configuration,
        ILogger<GraphPrincipalPicker> logger)
    {
        ArgumentNullException.ThrowIfNull(credentialFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger;

        var userAssignedClientId = configuration["AZURE_CLIENT_ID"];
        var credential = credentialFactory.CreateCredential(userAssignedClientId);
        var graph = new GraphServiceClient(credential);

        _fetchUsers = (filter, top, ct) => graph.Users.GetAsync(rq =>
        {
            rq.QueryParameters.Filter = filter;
            rq.QueryParameters.Top = top;
            rq.QueryParameters.Select = ["id", "displayName", "userPrincipalName", "mail"];
        }, ct);

        _fetchGroups = (filter, top, ct) => graph.Groups.GetAsync(rq =>
        {
            rq.QueryParameters.Filter = filter;
            rq.QueryParameters.Top = top;
            rq.QueryParameters.Select = ["id", "displayName", "mail"];
        }, ct);
    }

    // Test seam — direct delegate injection, matches the GraphClient pattern.
    internal GraphPrincipalPicker(
        Func<string, int, CancellationToken, Task<UserCollectionResponse?>> fetchUsers,
        Func<string, int, CancellationToken, Task<GroupCollectionResponse?>> fetchGroups,
        ILogger<GraphPrincipalPicker>? logger = null)
    {
        _fetchUsers = fetchUsers ?? throw new ArgumentNullException(nameof(fetchUsers));
        _fetchGroups = fetchGroups ?? throw new ArgumentNullException(nameof(fetchGroups));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GraphPrincipalPicker>.Instance;
    }

    [LoggerMessage(EventId = 8301, Level = LogLevel.Warning, Message = "Graph picker query failed (kind {Kind}, status {Status})")]
    private partial void LogGraphFailure(string kind, int status);

    public async Task<IReadOnlyList<PrincipalPickerItem>> SearchAsync(
        string query,
        int top,
        bool includeGroups,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<PrincipalPickerItem>();
        }

        var clampedTop = Math.Clamp(top, 1, 25);
        var safeQuery = query.Replace("'", "''", StringComparison.Ordinal);

        var users = await SearchUsersAsync(safeQuery, clampedTop, cancellationToken).ConfigureAwait(false);
        var groups = includeGroups
            ? await SearchGroupsAsync(safeQuery, clampedTop, cancellationToken).ConfigureAwait(false)
            : Array.Empty<PrincipalPickerItem>();

        return users
            .Concat(groups)
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .Take(clampedTop)
            .ToArray();
    }

    private async Task<IReadOnlyList<PrincipalPickerItem>> SearchUsersAsync(
        string safeQuery,
        int top,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _fetchUsers(
                $"startsWith(displayName,'{safeQuery}') or startsWith(mail,'{safeQuery}') or startsWith(userPrincipalName,'{safeQuery}')",
                top,
                cancellationToken).ConfigureAwait(false);

            if (response?.Value is null)
            {
                return Array.Empty<PrincipalPickerItem>();
            }

            return response.Value
                .Where(u => Guid.TryParse(u.Id, out _))
                .Select(u => new PrincipalPickerItem(
                    ObjectId: Guid.Parse(u.Id!),
                    PrincipalType: PrincipalType.User,
                    DisplayName: u.DisplayName ?? u.UserPrincipalName ?? "(no display name)",
                    Mail: u.Mail,
                    UserPrincipalName: u.UserPrincipalName))
                .ToArray();
        }
        catch (ODataError ex)
        {
            LogGraphFailure("users", ex.ResponseStatusCode);
            throw new GraphPickerException(
                $"Graph query for users failed with status {ex.ResponseStatusCode}.", ex);
        }
    }

    private async Task<IReadOnlyList<PrincipalPickerItem>> SearchGroupsAsync(
        string safeQuery,
        int top,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _fetchGroups(
                $"startsWith(displayName,'{safeQuery}')",
                top,
                cancellationToken).ConfigureAwait(false);

            if (response?.Value is null)
            {
                return Array.Empty<PrincipalPickerItem>();
            }

            return response.Value
                .Where(g => Guid.TryParse(g.Id, out _))
                .Select(g => new PrincipalPickerItem(
                    ObjectId: Guid.Parse(g.Id!),
                    PrincipalType: PrincipalType.Group,
                    DisplayName: g.DisplayName ?? "(no display name)",
                    Mail: g.Mail,
                    UserPrincipalName: null))
                .ToArray();
        }
        catch (ODataError ex)
        {
            LogGraphFailure("groups", ex.ResponseStatusCode);
            throw new GraphPickerException(
                $"Graph query for groups failed with status {ex.ResponseStatusCode}.", ex);
        }
    }
}

public sealed class GraphPickerException : Exception
{
    public GraphPickerException(string message, Exception innerException) : base(message, innerException) { }
}
