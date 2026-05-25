using BusTerminal.Api.Domain.Lifecycle;
using BusTerminal.Api.Domain.Relationships;

namespace BusTerminal.Api.Domain.Serialization;

// Spec 004 / FR-016. Matches contracts/import-export-envelope.schema.json.
// `Relationships` carries Relationship peer documents (US3 T102) — the Phase 2
// `RelationshipDocument` placeholder has been replaced now that the real record
// exists.
public sealed record ImportExportEnvelope(
    DateTimeOffset ExportedAt,
    IReadOnlyCollection<Resource> Resources,
    IReadOnlyCollection<Relationship> Relationships,
    string SchemaVersion = "2026-05-23",
    PrincipalReference? ExportedBy = null,
    string? SourceSystem = null,
    IReadOnlyCollection<ChangeEvent>? ChangeEvents = null,
    string? ConflictResolution = null);
