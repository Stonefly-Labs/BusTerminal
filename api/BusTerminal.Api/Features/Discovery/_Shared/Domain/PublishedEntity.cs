using System.Text.Json;

namespace BusTerminal.Api.Features.Discovery.Shared.Domain;

// Spec 009 / data-model.md §1.1 + §3. In-memory projection of the full
// published entity. Cosmos persistence translates this to/from the JSON
// document shape via PublishedEntityDocument (Phase 2 T015 piece).
//
// Curated fields (description, businessPurpose, tags, documentationLinks,
// contactInformation, operationalNotes) live in the `Registry` block — they
// are spec 006 / 008 fields that survive every discovery upsert.
public sealed record PublishedEntity(
    string Id,
    string SchemaVersion,
    EntityType EntityType,
    string Environment,
    string NamespaceId,
    string Name,
    string DisplayName,
    string CompositeKey,
    string? ParentEntityId,
    EntityRegistryMetadata Registry,
    LifecycleStatus LifecycleStatus,
    DateTimeOffset LifecycleStatusChangedUtc,
    DateTimeOffset FirstDiscoveredUtc,
    DateTimeOffset LastSeenUtc,
    string LastDiscoveryRunId,
    AzureSourcedEntity AzureSourced,
    string AzureSourcedHash,
    IReadOnlyList<EntityServiceAssociation> ServiceAssociations,
    DateTimeOffset CreatedUtc,
    string CreatedBy,
    DateTimeOffset LastModifiedUtc,
    string LastModifiedBy,
    string ETag)
{
    public const string CurrentSchemaVersion = "1.1";

    // Convenience derived projections — kept on the document for AI Search
    // filtering per data-model.md §1.1 ("Derived projection arrays").
    public IReadOnlyList<string> AssociatedServiceIds =>
        ServiceAssociations.Select(a => a.ServiceId).Distinct(StringComparer.Ordinal).ToArray();

    public IReadOnlyList<EntityServiceRole> AssociationRoles =>
        ServiceAssociations.Select(a => a.Role).Distinct().ToArray();
}

// Spec 006/008 curated metadata block carried alongside the spec 009 fields
// per data-model.md §1.1. Tags + documentationLinks are simple structures
// the registry catalog already supports; contactInformation + the long-form
// text fields are operator-authored.
public sealed record EntityRegistryMetadata(
    string? Description,
    string? BusinessPurpose,
    IReadOnlyList<string> Tags,
    IReadOnlyList<EntityDocumentationLink> DocumentationLinks,
    EntityContactInformation? ContactInformation,
    string? OperationalNotes)
{
    public static EntityRegistryMetadata Empty { get; } = new(
        Description: null,
        BusinessPurpose: null,
        Tags: Array.Empty<string>(),
        DocumentationLinks: Array.Empty<EntityDocumentationLink>(),
        ContactInformation: null,
        OperationalNotes: null);
}

public sealed record EntityDocumentationLink(string Label, string Url);

public sealed record EntityContactInformation(string? PrimaryContact, string? EscalationPath);

// Spec 009 / data-model.md §1.1 — the JsonElement is preserved as-is when a
// PublishedEntity round-trips through Cosmos so unknown fields survive forward
// schema evolution. Currently unused but reserved for future additive fields.
public sealed record PublishedEntityRaw(JsonElement Document);
