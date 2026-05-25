using System.Text.Json;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Validation;

namespace BusTerminal.Api.Domain.Serialization;

// Spec 004 / Q4. Materializes an UnknownResource from the raw JsonElement when the
// `resourceType` discriminator is not in the registry. Base Resource fields are
// extracted from the JSON using the same serializer options the converter uses,
// then the raw payload is preserved on `RawJson` for diagnostic surfacing.
//
// Required base fields that are missing or malformed bubble up as JsonException —
// the document is then truly broken and the caller's CreateAsync / read pipeline
// surfaces the error. UnknownResource is for unknown-type round-trip, not for
// rescuing malformed documents.
public static class UnknownResourceFactory
{
    public static Resource Create(string discriminator, JsonElement raw, JsonSerializerOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(discriminator);
        ArgumentNullException.ThrowIfNull(options);

        var id = GetRequired<ResourceId>(raw, "id", options);
        var name = GetRequired<ResourceName>(raw, "name", options);
        var displayName = raw.GetProperty("displayName").GetString()
            ?? throw new JsonException("UnknownResource: 'displayName' must be a non-null string.");
        var namespacePath = GetRequired<NamespacePath>(raw, "namespacePath", options);
        var lifecycle = GetRequired<LifecycleState>(raw, "lifecycle", options);
        var version = GetRequired<SemanticVersion>(raw, "version", options);
        var audit = GetRequired<AuditRecord>(raw, "audit", options);

        return new UnknownResource
        {
            Id = id,
            ResourceType = discriminator,
            Name = name,
            DisplayName = displayName,
            Description = TryGetString(raw, "description"),
            NamespacePath = namespacePath,
            Environments = TryGet<IReadOnlyCollection<EnvironmentClassification>>(raw, "environments", options) ?? [],
            Lifecycle = lifecycle,
            Version = version,
            Ownership = TryGet<OwnershipRecord>(raw, "ownership", options),
            Audit = audit,
            Classification = TryGet<ClassificationMetadata>(raw, "classification", options),
            Tags = TryGet<IReadOnlyCollection<TagReference>>(raw, "tags", options) ?? [],
            Extensions = TryGet<Extensions>(raw, "extensions", options) ?? Extensions.Empty,
            Documentation = TryGet<IReadOnlyCollection<DocumentationReference>>(raw, "documentation", options) ?? [],
            ValidationState = TryGet<ValidationResult>(raw, "validationState", options),
            ConcurrencyToken = raw.TryGetProperty("_etag", out var etag) && etag.ValueKind == JsonValueKind.String
                ? new ConcurrencyToken(etag.GetString()!)
                : ConcurrencyToken.Empty,
            IsDeleted = raw.TryGetProperty("isDeleted", out var deleted) && deleted.ValueKind == JsonValueKind.True,
            RawJson = raw,
        };
    }

    private static T GetRequired<T>(JsonElement root, string propertyName, JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            throw new JsonException($"UnknownResource: required property '{propertyName}' is missing.");
        }

        return element.Deserialize<T>(options)
            ?? throw new JsonException($"UnknownResource: required property '{propertyName}' deserialized to null.");
    }

    private static T? TryGet<T>(JsonElement root, string propertyName, JsonSerializerOptions options)
        where T : class =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind != JsonValueKind.Null
            ? element.Deserialize<T>(options)
            : null;

    private static string? TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
