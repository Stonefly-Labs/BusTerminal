using BusTerminal.Api.Domain.Lifecycle;

namespace BusTerminal.Api.Domain.Serialization;

// Spec 004 / FR-016. Matches contracts/import-export-envelope.schema.json.
// `RelationshipDocument` is the peer document type for relationships (introduced
// fully in US3 T102); a lightweight placeholder lives here so the envelope can
// compile in Phase 2 without referencing the not-yet-built Relationship type.
public sealed record ImportExportEnvelope(
    DateTimeOffset ExportedAt,
    IReadOnlyCollection<Resource> Resources,
    IReadOnlyCollection<RelationshipDocument> Relationships,
    string SchemaVersion = "2026-05-23",
    PrincipalReference? ExportedBy = null,
    string? SourceSystem = null,
    IReadOnlyCollection<ChangeEvent>? ChangeEvents = null,
    string? ConflictResolution = null);

// Placeholder for the US3 Relationship peer document. Replaced or extended by
// T102 when the full Relationship record lands. Kept minimal here so Phase 2
// foundational work compiles end-to-end without forward-referencing US3.
public sealed record RelationshipDocument(
    ResourceId Id,
    ResourceId SourceId,
    ResourceId TargetId,
    string Type,
    AuditRecord Audit,
    bool IsDeleted = false,
    ConcurrencyToken ConcurrencyToken = default);
