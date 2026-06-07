using System.Text.Json;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T038 / FR-020 / research §8. Given the persisted (current) entity
// and the submitted entity, produce the wire-shape ConflictResponse including
// the field-level diff. Diff strategy: serialize both shapes via the registry
// STJ options, walk the JSON objects, and emit one entry per differing property.
//
// The mapper is stateless and pure — registered as a singleton.
public sealed class ConcurrencyConflictMapper
{
    public ConflictResponse BuildConflict(
        IRegistryEntity currentEntity,
        IRegistryEntity submittedEntity,
        string submittedEtag,
        string? instance = null)
    {
        ArgumentNullException.ThrowIfNull(currentEntity);
        ArgumentNullException.ThrowIfNull(submittedEntity);

        // Build a JSON projection of each side so the diff walker doesn't need
        // to know the runtime shape (works uniformly across concrete types).
        var currentJson = JsonSerializer.SerializeToElement(currentEntity, currentEntity.GetType(), RegistryJsonOptions.Default);
        var submittedJson = JsonSerializer.SerializeToElement(submittedEntity, submittedEntity.GetType(), RegistryJsonOptions.Default);

        var changes = new List<ConflictChangedField>();
        DiffObject(currentJson, submittedJson, pointer: string.Empty, changes);

        return new ConflictResponse(
            EntityId: currentEntity.Id,
            CurrentVersion: currentEntity.Etag ?? string.Empty,
            SubmittedVersion: submittedEtag,
            CurrentEntity: currentEntity,
            ChangedFields: changes,
            Detail: "The entity was modified by another user since you loaded it.",
            Instance: instance);
    }

    private static void DiffObject(JsonElement current, JsonElement submitted, string pointer, List<ConflictChangedField> changes)
    {
        // Walk the union of property names across both sides so additions and
        // removals show up symmetrically.
        var properties = new HashSet<string>(StringComparer.Ordinal);
        if (current.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in current.EnumerateObject()) properties.Add(p.Name);
        }
        if (submitted.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in submitted.EnumerateObject()) properties.Add(p.Name);
        }

        foreach (var name in properties)
        {
            // Internal/server-managed fields don't surface to the operator —
            // skip them so the change list reflects user-meaningful changes.
            if (name is "_etag" or "etag" or "createdAtUtc" or "updatedAtUtc" or "fullyQualifiedName")
            {
                continue;
            }

            var hasCurrent = current.ValueKind == JsonValueKind.Object && current.TryGetProperty(name, out var c);
            var hasSubmitted = submitted.ValueKind == JsonValueKind.Object && submitted.TryGetProperty(name, out var s);

            current.TryGetProperty(name, out var currentValue);
            submitted.TryGetProperty(name, out var submittedValue);

            var subPointer = $"{pointer}/{name}";

            if (!hasCurrent || !hasSubmitted)
            {
                changes.Add(new ConflictChangedField(
                    subPointer,
                    hasCurrent ? Unwrap(currentValue) : null,
                    hasSubmitted ? Unwrap(submittedValue) : null));
                continue;
            }

            // Same property on both sides — compare by raw JSON text so
            // numeric/string/null/array shapes all compare correctly.
            if (!ElementsEqual(currentValue, submittedValue))
            {
                changes.Add(new ConflictChangedField(
                    subPointer,
                    Unwrap(currentValue),
                    Unwrap(submittedValue)));
            }
        }
    }

    private static bool ElementsEqual(JsonElement a, JsonElement b)
    {
        // Cheap structural compare via canonical re-serialization — STJ's
        // RawText preserves insignificant whitespace differently across calls,
        // so re-serializing through a single options instance normalizes.
        var aText = JsonSerializer.Serialize(a, RegistryJsonOptions.Default);
        var bText = JsonSerializer.Serialize(b, RegistryJsonOptions.Default);
        return string.Equals(aText, bText, StringComparison.Ordinal);
    }

    // Convert a JsonElement back to a plain CLR shape so the JSON response
    // serializer doesn't double-encode it.
    private static object? Unwrap(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var i) ? i : e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Undefined => null,
        _ => e.Clone(),
    };
}
