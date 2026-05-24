using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-015 latest-state. Full change history lives in ChangeEvent (Q5).
// Matches contracts/audit.schema.json.

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(HumanPrincipalReference), "human")]
[JsonDerivedType(typeof(WorkloadPrincipalReference), "workload")]
[JsonDerivedType(typeof(SystemPrincipalReference), "system")]
public abstract record PrincipalReference;

public sealed record HumanPrincipalReference(Guid ObjectId, string? DisplayName = null) : PrincipalReference;

public sealed record WorkloadPrincipalReference(Guid ObjectId, string? DisplayName = null) : PrincipalReference;

public sealed record SystemPrincipalReference(string SystemName) : PrincipalReference;

public sealed record SyncMetadata(
    string UpstreamSystem,
    string UpstreamId,
    DateTimeOffset LastSyncedAt);

public sealed record AuditRecord(
    PrincipalReference CreatedBy,
    DateTimeOffset CreatedAt,
    PrincipalReference ModifiedBy,
    DateTimeOffset ModifiedAt,
    string? SourceSystem = null,
    SyncMetadata? Synchronization = null);
