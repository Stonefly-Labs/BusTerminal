namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-017. Matches contracts/resources/environment.schema.json.
// Class name `EnvironmentResource` avoids clash with `System.Environment`; the
// persisted resourceType discriminator remains "environment" (registered with
// the registry under that string).
//
// Both the base `Resource.Classification` (ClassificationMetadata? — operational
// classification block, null on non-operational types) and this type's
// `Classification` (the EnvironmentClassification string per FR-017) want the
// same JSON wire-form property name. EnvironmentResource is a non-operational
// type (data-model.md §1), so the base block is always null here — `new` hides
// it cleanly and the wire form carries the env classification string.
public sealed record EnvironmentResource : Resource
{
    public new required EnvironmentClassification Classification { get; init; }

    public string? Region { get; init; }

    public string? ComplianceScope { get; init; }
}
