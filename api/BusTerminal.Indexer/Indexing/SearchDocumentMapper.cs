using System.Text.Json;

namespace BusTerminal.Indexer.Indexing;

// Spec 006 / T047 / contracts/indexer-events.md §3. Projects a Cosmos
// change-feed item into the AI Search index document shape defined in
// `contracts/search-index.json`. Tombstones bypass projection — the indexer
// calls DeleteDocumentsAsync(t._tombstoneFor) on those events.
public interface ISearchDocumentMapper
{
    IReadOnlyDictionary<string, object?> ToSearchDocument(RegistryEntityChangeFeedItem item);
}

public sealed class SearchDocumentMapper : ISearchDocumentMapper
{
    private const string BrokerKind = "AzureServiceBus";

    public IReadOnlyDictionary<string, object?> ToSearchDocument(RegistryEntityChangeFeedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // Tags: the index has a `tags` complex collection (matches the source
        // shape) AND a `tagKeysLower` collection (lowercase-dedup) AND the
        // `metadata` flattened-keys projection.
        var tags = item.Tags ?? Array.Empty<RegistryTagItem>();
        var tagKeysLower = tags
            .Select(t => t.Key?.ToLowerInvariant())
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var metadataFlat = item.Metadata.HasValue
            ? FlattenJson(item.Metadata.Value, prefix: string.Empty).ToArray()
            : Array.Empty<string>();

        // Null-or-empty normalizations per indexer-events.md §3 (description,
        // owner, azureResourceId — "null → empty string").
        return new Dictionary<string, object?>
        {
            ["id"] = item.Id,
            ["entityType"] = item.EntityType,
            ["name"] = item.Name ?? string.Empty,
            ["fullyQualifiedName"] = item.FullyQualifiedName ?? string.Empty,
            ["description"] = item.Description ?? string.Empty,
            ["owner"] = item.Owner ?? string.Empty,
            ["environment"] = item.Environment,
            ["status"] = item.Status,
            ["namespaceName"] = item.NamespaceName ?? string.Empty,
            ["azureResourceId"] = item.AzureResourceId ?? string.Empty,
            ["tags"] = tags.Select(t => new Dictionary<string, object?>
            {
                ["key"] = t.Key,
                ["value"] = t.Value,
            }).ToArray(),
            ["tagKeysLower"] = tagKeysLower,
            ["metadataFlat"] = metadataFlat,
            ["parentId"] = item.ParentId,
            ["updatedAtUtc"] = item.UpdatedAtUtc,
            ["createdAtUtc"] = item.CreatedAtUtc,
            ["brokerKind"] = BrokerKind,
        };
    }

    // Recursive `key=value` flattening per contracts/indexer-events.md §3.
    // Nested objects produce dot-path keys (`policy.retention.days=30`).
    // Arrays serialize as the raw JSON value string of the array.
    internal static IEnumerable<string> FlattenJson(JsonElement element, string prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var nextPrefix = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    foreach (var entry in FlattenJson(prop.Value, nextPrefix))
                    {
                        yield return entry;
                    }
                }
                break;

            case JsonValueKind.String:
                yield return $"{prefix}={element.GetString()}";
                break;

            case JsonValueKind.Number:
                yield return $"{prefix}={element.GetRawText()}";
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                yield return $"{prefix}={(element.ValueKind == JsonValueKind.True ? "true" : "false")}";
                break;

            case JsonValueKind.Null:
                yield return $"{prefix}=";
                break;

            case JsonValueKind.Array:
                // The contract calls for a single entry per leaf; arrays are
                // emitted as the raw JSON literal — discoverable via full-text
                // search without exploding the index width.
                yield return $"{prefix}={element.GetRawText()}";
                break;
        }
    }
}
