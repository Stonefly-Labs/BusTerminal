using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / data-model.md §3.1. Shared FluentValidation rules that every
// concrete entity-type validator composes. Each rule is exposed as an
// extension on `IRuleBuilderInitial<T, TProperty>` so per-type validators in
// T074 can mix-and-match (Namespace overrides `NameFormatRule` with the
// namespace charset; Queue/Topic keep the base form; etc.).
//
// Reasons each rule lives here rather than inline on a validator:
//   - The Zod schemas in `web/lib/registry/schemas.ts` mirror these constraints
//     directly; the shared-schema contract test (T061) walks both surfaces.
//   - The persistence layer (T029) shares the `Tag display normalization`
//     rule because tag canonicalization happens at write time, not validation
//     time — kept here so the rule definition stays in one place.
public static class RegistryEntityValidationRules
{
    // Base Azure-Service-Bus naming charset per data-model.md §3.1.
    // Per-entity-type rules narrow length and starting/ending charset on top.
    private static readonly Regex BaseNamePattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._\-/]{0,259}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Namespace name pattern per Azure Service Bus naming reference:
    // 6–50 chars, must start with letter, end alphanumeric, hyphens only inside.
    private static readonly Regex NamespaceNamePattern = new(
        @"^[A-Za-z][A-Za-z0-9\-]{4,48}[A-Za-z0-9]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // ARM resource-id shape: /subscriptions/{guid}/resourceGroups/{rg}/providers/{ns}/...
    // Plus a per-segment word-class check. The per-entity-type ARM-segment
    // consistency check (`.../namespaces/<ns>/queues/<name>` for a Queue, etc.)
    // is layered on top by `AzureResourceIdMatchesEntityType`.
    private static readonly Regex AzureResourceIdPattern = new(
        @"^/subscriptions/[0-9a-fA-F\-]{36}/resourceGroups/[^/]+/providers/[^/]+/[^/]+/[^/]+(/[^/]+/[^/]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Tag list defensive cap (data-model.md §3.1 — TagShapeRule).
    public const int MaxTagsPerEntity = 50;
    public const int MaxTagKeyLength = 256;
    public const int MaxTagValueLength = 1024;

    // Metadata size defensive cap (data-model.md §3.1 — MetadataSizeRule).
    // 100KB; well under Cosmos's 2MB doc limit so other fields have room.
    public const int MaxMetadataSerializedBytes = 100 * 1024;

    public const int MaxDescriptionLength = 4000;
    public const int MaxOwnerLength = 512;
    public const int MaxAzureResourceIdLength = 2048;
    public const int MaxEnvironmentLength = 64;
    public const int MaxNameLength = 260;

    // FR-003. The base required-field rule applied by every concrete validator.
    // `id`/`environment`/`name` are guarded individually so the failure message
    // points at the offending field.
    public static IRuleBuilderOptions<T, Guid> RequiredId<T>(this IRuleBuilder<T, Guid> builder)
        => builder.NotEqual(Guid.Empty).WithMessage("id is required.");

    public static IRuleBuilderOptions<T, string> RequiredEnvironment<T>(this IRuleBuilder<T, string> builder)
        => builder
            .NotEmpty().WithMessage("environment is required.")
            .MaximumLength(MaxEnvironmentLength)
            .WithMessage($"environment must be {MaxEnvironmentLength} characters or fewer.");

    public static IRuleBuilderOptions<T, string> RequiredName<T>(this IRuleBuilder<T, string> builder)
        => builder
            .NotEmpty().WithMessage("name is required.")
            .MaximumLength(MaxNameLength)
            .WithMessage($"name must be {MaxNameLength} characters or fewer.");

    // FR-015 + Edge Case "Special characters". Base pattern; per-type rules
    // tighten this further.
    public static IRuleBuilderOptions<T, string> BaseNameFormat<T>(this IRuleBuilder<T, string> builder)
        => builder.Must(name => name is not null && BaseNamePattern.IsMatch(name))
            .WithMessage("name does not match the base Azure Service Bus naming pattern.");

    public static IRuleBuilderOptions<T, string> NamespaceNameFormat<T>(this IRuleBuilder<T, string> builder)
        => builder.Must(name => name is not null && NamespaceNamePattern.IsMatch(name))
            .WithMessage("Namespace name must be 6–50 chars, start with a letter, end with a letter or digit, hyphens only inside.");

    public static IRuleBuilderOptions<T, string> MaxNameLengthFor<T>(this IRuleBuilder<T, string> builder, int max, string entityType)
        => builder.MaximumLength(max)
            .WithMessage($"{entityType} name must be {max} characters or fewer.");

    // FR-013a. Closed enum already prevents `Deleted` at the type level; this
    // rule guards against unknown values arriving via the wire path (model
    // binders sometimes coerce strings to the underlying integer).
    public static IRuleBuilderOptions<T, RegistryEntityStatus> StatusValue<T>(this IRuleBuilder<T, RegistryEntityStatus> builder)
        => builder.IsInEnum().WithMessage("status must be Active or Deprecated.");

    // FR-004. `Discovered` is reserved; only `Manual` is legal on the wire in
    // this slice.
    public static IRuleBuilderOptions<T, RegistrySource> SourceValue<T>(this IRuleBuilder<T, RegistrySource> builder)
        => builder.Equal(RegistrySource.Manual).WithMessage("source must be Manual.");

    // FR-015 / Edge Case "Invalid Azure resource IDs". When present, the ARM
    // id must match the ARM shape AND its resource-type segment must match the
    // entity type.
    public static IRuleBuilderOptions<T, string?> AzureResourceIdFormat<T>(
        this IRuleBuilder<T, string?> builder,
        RegistryEntityType entityType)
        => builder.Must(id => string.IsNullOrEmpty(id) ||
                              (id.Length <= MaxAzureResourceIdLength
                               && AzureResourceIdPattern.IsMatch(id)
                               && AzureResourceIdMatchesEntityType(id, entityType)))
            .WithMessage($"azureResourceId must be a well-formed ARM resource id matching entityType {entityType}.");

    public static IRuleBuilderOptions<T, IReadOnlyList<RegistryTag>?> TagShape<T>(this IRuleBuilder<T, IReadOnlyList<RegistryTag>?> builder)
        => builder.Must(tags =>
        {
            if (tags is null || tags.Count == 0) return true;
            if (tags.Count > MaxTagsPerEntity) return false;
            foreach (var t in tags)
            {
                if (t is null) return false;
                if (string.IsNullOrEmpty(t.Key) || t.Key.Length > MaxTagKeyLength) return false;
                if (string.IsNullOrEmpty(t.Value) || t.Value.Length > MaxTagValueLength) return false;
            }
            return true;
        }).WithMessage($"tags must have non-empty key/value pairs (keys ≤ {MaxTagKeyLength}, values ≤ {MaxTagValueLength}, ≤ {MaxTagsPerEntity} per entity).");

    public static IRuleBuilderOptions<T, JsonElement?> MetadataSize<T>(this IRuleBuilder<T, JsonElement?> builder)
        => builder.Must(metadata =>
        {
            if (!metadata.HasValue) return true;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(metadata.Value);
            return bytes.Length <= MaxMetadataSerializedBytes;
        }).WithMessage($"metadata serialized JSON must be ≤ {MaxMetadataSerializedBytes / 1024}KB.");

    public static IRuleBuilderOptions<T, string?> DescriptionLength<T>(this IRuleBuilder<T, string?> builder)
        => builder.MaximumLength(MaxDescriptionLength)
            .WithMessage($"description must be {MaxDescriptionLength} characters or fewer.");

    public static IRuleBuilderOptions<T, string?> OwnerLength<T>(this IRuleBuilder<T, string?> builder)
        => builder.MaximumLength(MaxOwnerLength)
            .WithMessage($"owner must be {MaxOwnerLength} characters or fewer.");

    // FR-012. On PUT, the submitted entityType must match the persisted one.
    // The caller passes the persisted value (read from Cosmos before the
    // ReplaceItemAsync call) so the rule has access to both sides.
    public static IRuleBuilderOptions<T, RegistryEntityType> EntityTypeMatches<T>(
        this IRuleBuilder<T, RegistryEntityType> builder,
        RegistryEntityType persisted)
        => builder.Equal(persisted).WithMessage("entityType is immutable after first save.");

    // FR-012. On PUT, the submitted id must match the URL `{id}` segment.
    public static IRuleBuilderOptions<T, Guid> IdMatches<T>(this IRuleBuilder<T, Guid> builder, Guid expected)
        => builder.Equal(expected).WithMessage("id is immutable and must match the route value.");

    // FR-005. `createdAtUtc` is immutable after first save.
    public static IRuleBuilderOptions<T, DateTimeOffset> CreatedAtImmutable<T>(
        this IRuleBuilder<T, DateTimeOffset> builder,
        DateTimeOffset persisted)
        => builder.Equal(persisted).WithMessage("createdAtUtc is immutable after first save.");

    // research §9. The display-normalization rule is a (transform, not a
    // hard validation rule) — when the entity already has a tag with key
    // matching the submitted key case-insensitively, the persisted casing is
    // the first-write casing. Centralized here because the API write path,
    // the Cosmos persistence layer, and the audit field-diff all consume it.
    public static IReadOnlyList<RegistryTag> NormalizeTagsForWrite(
        IReadOnlyList<RegistryTag>? submittedTags,
        IReadOnlyList<RegistryTag>? persistedTags)
    {
        if (submittedTags is null || submittedTags.Count == 0)
        {
            return Array.Empty<RegistryTag>();
        }

        // Build a lookup of persisted-key-first-case-by-lowercase so we can
        // re-stamp submitted keys to their persisted casing. Multi-value-per-key
        // is preserved: each submitted (key, value) is emitted independently.
        var persistedKeyByLower = new Dictionary<string, string>(StringComparer.Ordinal);
        if (persistedTags is not null)
        {
            foreach (var existing in persistedTags)
            {
                if (existing is null) continue;
                if (!persistedKeyByLower.ContainsKey(existing.TagKeyLower))
                {
                    persistedKeyByLower[existing.TagKeyLower] = existing.Key;
                }
            }
        }

        // First-write wins WITHIN the submission as well: among submitted tags
        // that share a key (case-insensitively), the casing of the first one
        // is the canonical casing for any matching persisted key.
        var canonicalCaseByLower = new Dictionary<string, string>(StringComparer.Ordinal);
        var output = new List<RegistryTag>(submittedTags.Count);
        foreach (var submitted in submittedTags)
        {
            if (submitted is null) continue;
            var lower = submitted.TagKeyLower;

            string canonicalKey;
            if (persistedKeyByLower.TryGetValue(lower, out var persistedCase))
            {
                canonicalKey = persistedCase;
            }
            else if (canonicalCaseByLower.TryGetValue(lower, out var alreadyCanonical))
            {
                canonicalKey = alreadyCanonical;
            }
            else
            {
                canonicalKey = submitted.Key;
                canonicalCaseByLower[lower] = canonicalKey;
            }

            output.Add(new RegistryTag(canonicalKey, submitted.Value));
        }

        return output;
    }

    private static bool AzureResourceIdMatchesEntityType(string azureResourceId, RegistryEntityType entityType)
    {
        // For a Namespace: /providers/Microsoft.ServiceBus/namespaces/<name>
        // Queue/Topic add /queues/<name> or /topics/<name>
        // Subscription/Rule chain deeper: .../topics/<t>/subscriptions/<s>[/rules/<r>]
        var expectedTypeSegment = entityType switch
        {
            RegistryEntityType.Namespace => "namespaces",
            RegistryEntityType.Queue => "queues",
            RegistryEntityType.Topic => "topics",
            RegistryEntityType.Subscription => "subscriptions",
            RegistryEntityType.Rule => "rules",
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unknown RegistryEntityType."),
        };

        // The expected segment must appear as the second-to-last segment of the path.
        // ARM ids end with `<type>/<name>` so the type token is at index Length-2.
        var segments = azureResourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2) return false;
        return string.Equals(segments[^2], expectedTypeSegment, StringComparison.OrdinalIgnoreCase);
    }
}
