using System.Text.Json;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T041. Entity ↔ request/response DTO converters. Centralizes:
//   - the `_overwriteAcknowledged` extraction (a request-body extension
//     consumed by UpdateEndpoint to mark a force-overwrite intent);
//   - server-side computation of `fullyQualifiedName` (data-model.md §2 —
//     read-only from the client's perspective);
//   - normalization of submitted tags via
//     RegistryEntityValidationRules.NormalizeTagsForWrite.
//
// Specific Create/Update request DTO records live in the per-entity-type
// slices (Namespaces/, Queues/, etc.) and are mapped here. This class is the
// stable seam between wire shapes and the canonical `RegistryEntity` runtime
// type.
public sealed class RegistryDtoMapping
{
    // Server-computed `fullyQualifiedName` per data-model.md §2.
    // Namespace: just the name.
    // Queue/Topic: `<namespaceName>/<name>`
    // Subscription: `<namespaceName>/<topicName>/<name>`
    // Rule: `<namespaceName>/<topicName>/<subscriptionName>/<name>`
    //
    // The caller passes the resolved ancestor name chain because the
    // persistence layer does not eagerly join across documents — endpoint
    // handlers query the parent chain when constructing the FQN.
    public string ComputeFullyQualifiedName(
        RegistryEntityType entityType,
        string name,
        string? namespaceName,
        string? topicName = null,
        string? subscriptionName = null)
    {
        return entityType switch
        {
            RegistryEntityType.Namespace => name,
            RegistryEntityType.Queue or RegistryEntityType.Topic => Join(namespaceName, name),
            RegistryEntityType.Subscription => Join(namespaceName, topicName, name),
            RegistryEntityType.Rule => Join(namespaceName, topicName, subscriptionName, name),
            _ => name,
        };
    }

    private static string Join(params string?[] parts) =>
        string.Join("/", parts.Where(p => !string.IsNullOrEmpty(p)));

    // Per FR-020 / research §8 — clients signal a force-overwrite by including
    // `_overwriteAcknowledged: true` in the PUT body. Endpoint handlers must
    // strip the flag before validation so the FluentValidation rule set isn't
    // confused by the extension property.
    public bool ExtractOverwriteAcknowledged(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object) return false;
        if (!body.TryGetProperty("_overwriteAcknowledged", out var prop)) return false;
        return prop.ValueKind == JsonValueKind.True;
    }

    // Centralized tag normalization at write time. Called by Create and
    // Update endpoint handlers before forwarding to the persistence layer.
    public IReadOnlyList<RegistryTag> NormalizeTags(
        IReadOnlyList<RegistryTag>? submitted,
        IReadOnlyList<RegistryTag>? persisted)
        => RegistryEntityValidationRules.NormalizeTagsForWrite(submitted, persisted);
}
