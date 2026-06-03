using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Infrastructure.Credentials;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Search;

// Spec 006 / T036 / research §7. AAD-backed adapter over Azure.Search.Documents
// SearchClient. The workload UAMI's `Search Index Data Reader` role at the
// service scope (granted in T016) is sufficient for the read path; the
// indexer carries the `Search Index Data Contributor` role for writes.
public sealed class AzureAiSearchClient : ISearchClient
{
    private readonly SearchClient _client;

    public AzureAiSearchClient(
        IOptions<AiSearchOptions> options,
        IAzureCredentialFactory credentialFactory,
        IConfiguration configuration)
    {
        var opts = options.Value;
        var userAssignedClientId = configuration["AZURE_CLIENT_ID"];
        var credential = credentialFactory.CreateCredential(userAssignedClientId);
        _client = new SearchClient(new Uri(opts.Endpoint), opts.IndexName, credential);
    }

    public async Task<RegistrySearchResults> SearchAsync(
        RegistrySearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var searchOptions = new SearchOptions
        {
            Skip = request.Skip,
            Size = request.Top,
            IncludeTotalCount = true,
            // Stable tiebreaker (research §13). The runtime Sort enum maps to
            // OData $orderby tokens; `id asc` is appended unconditionally so
            // repeated identical queries return rows in the same order.
        };

        // Tiebreaker — `Search.Score()` is the BM25 relevance score; only
        // applicable in Relevance mode.
        switch (request.Sort)
        {
            case RegistrySearchSort.UpdatedAtDesc:
                searchOptions.OrderBy.Add("updatedAtUtc desc");
                break;
            case RegistrySearchSort.UpdatedAtAsc:
                searchOptions.OrderBy.Add("updatedAtUtc asc");
                break;
            case RegistrySearchSort.NameAsc:
                searchOptions.OrderBy.Add("name asc");
                break;
            case RegistrySearchSort.NameDesc:
                searchOptions.OrderBy.Add("name desc");
                break;
            case RegistrySearchSort.Relevance:
            default:
                searchOptions.OrderBy.Add("updatedAtUtc desc");
                break;
        }
        searchOptions.OrderBy.Add("id asc");

        var filters = BuildFilter(request);
        if (!string.IsNullOrEmpty(filters))
        {
            searchOptions.Filter = filters;
        }

        var query = string.IsNullOrWhiteSpace(request.Query) ? "*" : request.Query;
        var response = await _client.SearchAsync<RegistrySearchDocument>(query, searchOptions, cancellationToken).ConfigureAwait(false);
        var hits = new List<RegistrySearchHit>();
        await foreach (var result in response.Value.GetResultsAsync().ConfigureAwait(false))
        {
            var doc = result.Document;
            hits.Add(new RegistrySearchHit(
                Id: Guid.TryParse(doc.Id, out var id) ? id : Guid.Empty,
                EntityType: ParseEntityType(doc.EntityType),
                Name: doc.Name,
                FullyQualifiedName: doc.FullyQualifiedName,
                Environment: doc.Environment,
                Status: doc.Status,
                Owner: doc.Owner,
                NamespaceName: doc.NamespaceName,
                ParentId: Guid.TryParse(doc.ParentId, out var pid) ? pid : null,
                Score: result.Score));
        }

        return new RegistrySearchResults(hits, response.Value.TotalCount);
    }

    public async Task<IReadOnlyList<RegistrySuggestion>> SuggestAsync(
        string partialText,
        int top,
        string? environmentFilter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(partialText))
        {
            return Array.Empty<RegistrySuggestion>();
        }

        // The contract reserves SuggestAsync for the cmdk typeahead; v1 uses
        // a lightweight prefix-search shape rather than an explicit Suggester
        // since no Suggester is defined in the index (`suggesters: []` per
        // contracts/search-index.json). Phase 4 (T112) may swap this for a
        // configured Suggester if relevance demands it.
        var searchOptions = new SearchOptions
        {
            Size = Math.Clamp(top, 1, 25),
            Select = { "id", "name", "entityType", "environment" },
        };
        searchOptions.OrderBy.Add("name asc");
        searchOptions.OrderBy.Add("id asc");
        if (!string.IsNullOrEmpty(environmentFilter))
        {
            searchOptions.Filter = $"environment eq '{EscapeOData(environmentFilter)}'";
        }

        var query = $"{partialText}*";
        var response = await _client.SearchAsync<RegistrySearchDocument>(query, searchOptions, cancellationToken).ConfigureAwait(false);
        var suggestions = new List<RegistrySuggestion>();
        await foreach (var result in response.Value.GetResultsAsync().ConfigureAwait(false))
        {
            var doc = result.Document;
            suggestions.Add(new RegistrySuggestion(
                Id: Guid.TryParse(doc.Id, out var id) ? id : Guid.Empty,
                Suggested: doc.Name,
                EntityType: ParseEntityType(doc.EntityType),
                Environment: doc.Environment ?? string.Empty));
        }

        return suggestions;
    }

    private static string? BuildFilter(RegistrySearchRequest request)
    {
        var clauses = new List<string>();
        if (request.EntityTypeFilter.HasValue)
        {
            clauses.Add($"entityType eq '{request.EntityTypeFilter.Value}'");
        }
        if (!string.IsNullOrEmpty(request.EnvironmentFilter))
        {
            clauses.Add($"environment eq '{EscapeOData(request.EnvironmentFilter)}'");
        }
        if (request.StatusFilter.HasValue)
        {
            clauses.Add($"status eq '{request.StatusFilter.Value}'");
        }
        if (request.TagKeysAnyLower is { Count: > 0 } keys)
        {
            var orList = string.Join(" or ", keys.Select(k => $"k eq '{EscapeOData(k)}'"));
            clauses.Add($"tagKeysLower/any(k: {orList})");
        }
        if (request.TagsAny is { Count: > 0 } tags)
        {
            var orList = string.Join(" or ", tags.Select(t =>
                $"(t/key eq '{EscapeOData(t.Key)}' and t/value eq '{EscapeOData(t.Value)}')"));
            clauses.Add($"tags/any(t: {orList})");
        }

        return clauses.Count == 0 ? null : string.Join(" and ", clauses);
    }

    private static string EscapeOData(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static RegistryEntityType ParseEntityType(string? token) =>
        Enum.TryParse<RegistryEntityType>(token, out var et) ? et : RegistryEntityType.Namespace;

    // Internal shape received from the AI Search SDK. Field names match the
    // index schema in contracts/search-index.json.
    private sealed record RegistrySearchDocument
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("entityType")] public string? EntityType { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("fullyQualifiedName")] public string? FullyQualifiedName { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("environment")] public string? Environment { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("status")] public string? Status { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("owner")] public string? Owner { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("namespaceName")] public string? NamespaceName { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("parentId")] public string? ParentId { get; init; }
    }
}
