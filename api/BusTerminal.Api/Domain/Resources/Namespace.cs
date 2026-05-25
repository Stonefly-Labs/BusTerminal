namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-003. Matches contracts/resources/namespace.schema.json.
// InheritedMetadata (computed by NamespaceInheritance service) is NOT persisted —
// it's resolved lazily from the parent chain at read time, so it is not on this record.
public sealed record Namespace : Resource
{
    public ResourceId? ParentNamespaceId { get; init; }
}
