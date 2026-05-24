using System.Text.Json;
using BusTerminal.Api.Domain.Validation;

namespace BusTerminal.Api.Domain.Relationships;

// Spec 004 / FR-008 / T102. Peer document type — NOT a Resource subtype.
// Stored in the same `resources` Cosmos container under partition key
// `resourceType` = "relationship" so traversal queries can hit one container.
//
// Matches contracts/relationship.schema.json. `Annotations` is an open
// JsonElement so callers can attach edge metadata without a schema fork.
public sealed record Relationship
{
    public required ResourceId Id { get; init; }

    // Cosmos partition key value — must always be the literal "relationship".
    public string ResourceType { get; init; } = ResourceTypeDiscriminators.Relationship;

    public required ResourceId SourceId { get; init; }

    public required ResourceId TargetId { get; init; }

    public required RelationshipType Type { get; init; }

    public JsonElement? Annotations { get; init; }

    public required AuditRecord Audit { get; init; }

    public ValidationResult? ValidationState { get; init; }

    public bool IsDeleted { get; init; }

    public ConcurrencyToken ConcurrencyToken { get; init; } = ConcurrencyToken.Empty;
}

// Direction filter used by traversal + relationship queries.
public enum Direction
{
    Outbound,
    Inbound,
    Both,
}
