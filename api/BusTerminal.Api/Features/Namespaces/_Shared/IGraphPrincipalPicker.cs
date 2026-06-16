namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / research §2 + contracts/namespace-onboarding-api.yaml#/_picker.
// Adapter port for the Entra picker — searches users via
// graph.Users.GetAsync($filter, $top=25), groups via graph.Groups.GetAsync,
// merges + display-name-ascending sort. Implementation lives in
// Infrastructure/Graph/GraphPrincipalPicker.cs.
//
// Caller behaviour: the wizard's Entra picker hits this via the
// /api/namespaces/_picker proxy endpoint with a debounced search-as-you-type
// query (research §13). Empty query → empty result.
public interface IGraphPrincipalPicker
{
    Task<IReadOnlyList<PrincipalPickerItem>> SearchAsync(
        string query,
        int top,
        bool includeGroups,
        CancellationToken cancellationToken);
}

// Spec 008 / contracts/namespace-onboarding-api.yaml#/PrincipalPickerItem.
public sealed record PrincipalPickerItem(
    Guid ObjectId,
    PrincipalType PrincipalType,
    string DisplayName,
    string? Mail,
    string? UserPrincipalName);
