namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-002 + research §9. Free-form key/value pair. Keys are matched
// case-insensitively (first-write wins for display casing); values are matched
// and displayed case-preserved. The persisted shape is an array of these
// records (not a JSON object) to permit multi-value-per-key semantics.
public sealed record RegistryTag(string Key, string Value)
{
    // Lowercase-key projection consumed by the search index `tagKeysLower`
    // field (data-model.md §6.1) and by the duplicate-tag detector inside
    // TagDisplayNormalizationRule. Invariant lowercase keeps the projection
    // stable across locales (Turkish-i etc.).
    public string TagKeyLower => Key.ToLowerInvariant();
}
