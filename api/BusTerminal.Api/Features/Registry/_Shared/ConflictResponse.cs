namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-020 / research §8. RFC 7807 problem document with two
// registry-specific extension members (`currentEntity`, `changedFields`).
// Wire shape is fixed by
// `specs/006-service-bus-registry-core/contracts/conflict-response.schema.json`.
// The literal `Type`, `Title`, `Status`, and `Code` values are constants so
// drift from the schema fails the contract tests (T061, T082).
public sealed record ConflictResponse(
    Guid EntityId,
    string CurrentVersion,
    string SubmittedVersion,
    IRegistryEntity CurrentEntity,
    IReadOnlyList<ConflictChangedField> ChangedFields,
    string? Detail = null,
    string? Instance = null)
{
    public string Type { get; } = "https://busterminal.dev/probs/concurrency-conflict";
    public string Title { get; } = "Concurrency conflict";
    public int Status { get; } = 409;
    public string Code { get; } = "ConcurrencyConflict";
}

// Per-field entry in `changedFields`. `field` is a JSON pointer; `currentValue`
// and `submittedValue` are opaque (any JSON shape, including null) by contract.
public sealed record ConflictChangedField(string Field, object? CurrentValue, object? SubmittedValue);
