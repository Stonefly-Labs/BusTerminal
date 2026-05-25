namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-002. Matches contracts/resources/tag.schema.json.
// Class name `TagResource` distinguishes from the `TagReference` value type
// (in-document pointer) — TagResource is the first-class catalog entry.
public sealed record TagResource : Resource
{
    public string? Category { get; init; }

    public string? Color { get; init; }
}
