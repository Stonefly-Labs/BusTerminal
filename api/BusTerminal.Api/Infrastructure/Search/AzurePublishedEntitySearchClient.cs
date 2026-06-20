using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Infrastructure.Credentials;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Search;

// Spec 009 / T070. AAD-backed adapter over the `registry-entities-v1` index
// for the published-entity surface. Mirrors AzureAiSearchClient's wiring
// (UAMI via DefaultAzureCredential) but operates over the spec 009 typed
// shapes — string ids, EntityType (4 values), LifecycleStatus, the
// derived projection arrays (associatedServiceIds, associationRoles).
//
// FR-008 / data-model.md §1.1 invariant: PublishedEntity docs ALWAYS carry
// `lifecycleStatus`; legacy spec 006 Queue/Topic/Subscription/Rule docs do
// not. The `lifecycleStatus ne null` filter is appended unconditionally so
// the published-entity catalog never accidentally surfaces a spec 006-only
// document.
public sealed class AzurePublishedEntitySearchClient : IPublishedEntitySearchClient
{
    private readonly SearchClient _client;

    public AzurePublishedEntitySearchClient(
        IOptions<AiSearchOptions> options,
        IAzureCredentialFactory credentialFactory,
        IConfiguration configuration)
    {
        var opts = options.Value;
        var userAssignedClientId = configuration["AZURE_CLIENT_ID"];
        var credential = credentialFactory.CreateCredential(userAssignedClientId);
        _client = new SearchClient(new Uri(opts.Endpoint), opts.IndexName, credential);
    }

    public async Task<PublishedEntitySearchResults> SearchAsync(
        PublishedEntitySearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var searchOptions = new SearchOptions
        {
            Skip = request.Skip,
            Size = request.Top,
            IncludeTotalCount = true,
        };

        switch (request.Sort)
        {
            case PublishedEntitySearchSort.NameAsc:
                searchOptions.OrderBy.Add("name asc");
                break;
            case PublishedEntitySearchSort.NameDesc:
                searchOptions.OrderBy.Add("name desc");
                break;
            case PublishedEntitySearchSort.LastSeenAsc:
                searchOptions.OrderBy.Add("lastSeenUtc asc");
                break;
            case PublishedEntitySearchSort.LastSeenDesc:
                searchOptions.OrderBy.Add("lastSeenUtc desc");
                break;
        }
        searchOptions.OrderBy.Add("id asc");

        var filter = BuildFilter(request);
        if (!string.IsNullOrEmpty(filter))
        {
            searchOptions.Filter = filter;
        }

        var query = string.IsNullOrWhiteSpace(request.Query) ? "*" : request.Query;
        var response = await _client.SearchAsync<PublishedEntitySearchDocument>(
            query, searchOptions, cancellationToken).ConfigureAwait(false);

        var hits = new List<PublishedEntitySearchHit>();
        await foreach (var result in response.Value.GetResultsAsync().ConfigureAwait(false))
        {
            var doc = result.Document;
            hits.Add(new PublishedEntitySearchHit(
                Id: doc.Id ?? string.Empty,
                EntityType: ParseEntityType(doc.EntityType),
                NamespaceId: doc.NamespaceId ?? string.Empty,
                Name: doc.Name ?? string.Empty,
                ParentEntityId: doc.ParentEntityId,
                LifecycleStatus: ParseLifecycle(doc.LifecycleStatus),
                LastSeenUtc: doc.LastSeenUtc,
                Environment: doc.Environment,
                AssociatedServiceIds: doc.AssociatedServiceIds ?? Array.Empty<string>(),
                AssociationRoles: (doc.AssociationRoles ?? Array.Empty<string>())
                    .Select(ParseRole)
                    .Where(r => r.HasValue)
                    .Select(r => r!.Value)
                    .ToArray(),
                Tags: doc.Tags ?? Array.Empty<string>()));
        }

        return new PublishedEntitySearchResults(hits, response.Value.TotalCount ?? 0);
    }

    // Build the OData $filter clause. The spec 009 filters are AND-composed;
    // multi-value filters within a category (lifecycleStatus[], entityType[],
    // associationRole[]) are OR-composed. `lifecycleStatus ne null` is
    // unconditional — see class-level note.
    internal static string BuildFilter(PublishedEntitySearchRequest request)
    {
        var clauses = new List<string>
        {
            "lifecycleStatus ne null",
        };

        if (request.EntityTypeFilters is { Count: > 0 } types)
        {
            var orList = string.Join(" or ", types.Select(t => $"entityType eq '{t}'"));
            clauses.Add($"({orList})");
        }
        if (!string.IsNullOrEmpty(request.NamespaceIdFilter))
        {
            clauses.Add($"namespaceId eq '{EscapeOData(request.NamespaceIdFilter)}'");
        }
        if (!string.IsNullOrEmpty(request.AssociatedServiceIdFilter))
        {
            clauses.Add($"associatedServiceIds/any(s: s eq '{EscapeOData(request.AssociatedServiceIdFilter)}')");
        }
        if (request.AssociationRoleFilters is { Count: > 0 } roles)
        {
            var orList = string.Join(" or ", roles.Select(r => $"r eq '{r}'"));
            clauses.Add($"associationRoles/any(r: {orList})");
        }
        if (request.LifecycleStatusFilters is { Count: > 0 } statuses)
        {
            var orList = string.Join(" or ", statuses.Select(s => $"lifecycleStatus eq '{s}'"));
            clauses.Add($"({orList})");
        }
        if (request.TagFilters is { Count: > 0 } tags)
        {
            var orList = string.Join(" or ", tags.Select(t => $"t eq '{EscapeOData(t)}'"));
            clauses.Add($"tags/any(t: {orList})");
        }

        return string.Join(" and ", clauses);
    }

    private static string EscapeOData(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static EntityType ParseEntityType(string? token)
        => Enum.TryParse<EntityType>(token, out var t) ? t : EntityType.Queue;

    private static LifecycleStatus ParseLifecycle(string? token)
        => Enum.TryParse<LifecycleStatus>(token, out var s) ? s : LifecycleStatus.Active;

    private static EntityServiceRole? ParseRole(string token)
        => Enum.TryParse<EntityServiceRole>(token, out var r) ? r : null;

    // Internal shape received from the SDK. Field names match the extended
    // `registry-entities-v1` schema per data-model.md §2.1.
    private sealed record PublishedEntitySearchDocument
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")] public string? Id { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("entityType")] public string? EntityType { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("namespaceId")] public string? NamespaceId { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("name")] public string? Name { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("parentEntityId")] public string? ParentEntityId { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("lifecycleStatus")] public string? LifecycleStatus { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("lastSeenUtc")] public DateTimeOffset? LastSeenUtc { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("environment")] public string? Environment { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("associatedServiceIds")] public IReadOnlyList<string>? AssociatedServiceIds { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("associationRoles")] public IReadOnlyList<string>? AssociationRoles { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("tags")] public IReadOnlyList<string>? Tags { get; init; }
    }
}
