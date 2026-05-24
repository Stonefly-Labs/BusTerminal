namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-019. Matches contracts/resources/documentation-asset.schema.json.
public sealed record DocumentationAsset : Resource
{
    public required DocumentationAssetKind AssetKind { get; init; }

    public required string Uri { get; init; }

    public IReadOnlyCollection<ResourceId> AttachedResourceIds { get; init; } = [];
}
