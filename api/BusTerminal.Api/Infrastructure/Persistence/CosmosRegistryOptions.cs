using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 006 / data-model.md §4.1. Container names + RU bands (documentation
// only — RU bands are owned by IaC, not the runtime). Distinct from spec-004's
// CosmosOptions so the registry slice can evolve its layout without churning
// the canonical-store contract.
public sealed class CosmosRegistryOptions
{
    public const string SectionName = "CosmosRegistry";

    // The registry containers live on the same canonical database the spec-004
    // store uses. Repeating the database name here (rather than reusing
    // CosmosOptions.Database) keeps the registry slice's configuration
    // self-contained at deploy time.
    [Required]
    public string Database { get; set; } = "canonical";

    [Required]
    public string EntitiesContainer { get; set; } = "registry-entities";

    [Required]
    public string AuditContainer { get; set; } = "registry-audit";

    [Required]
    public string LeasesContainer { get; set; } = "registry-entities-leases";

    // Spec 008 / data-model.md §3 — append-only ValidationRun records
    // (PK /namespaceId, no TTL, lowest autoscale RU band per research §6).
    [Required]
    public string ValidationRunsContainer { get; set; } = "namespace-validation-runs";

    // Tombstone TTL in seconds — research §10. Cosmos item-level TTL deletes
    // the tombstone marker automatically; the indexer has the window between
    // write and TTL expiry to propagate the delete to AI Search.
    public int TombstoneTtlSeconds { get; set; } = 60;
}

internal sealed class CosmosRegistryOptionsValidator : IValidateOptions<CosmosRegistryOptions>
{
    public ValidateOptionsResult Validate(string? name, CosmosRegistryOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Database))
        {
            errors.Add("CosmosRegistry:Database must be set.");
        }
        if (string.IsNullOrWhiteSpace(options.EntitiesContainer))
        {
            errors.Add("CosmosRegistry:EntitiesContainer must be set.");
        }
        if (string.IsNullOrWhiteSpace(options.AuditContainer))
        {
            errors.Add("CosmosRegistry:AuditContainer must be set.");
        }
        if (string.IsNullOrWhiteSpace(options.LeasesContainer))
        {
            errors.Add("CosmosRegistry:LeasesContainer must be set.");
        }
        if (string.IsNullOrWhiteSpace(options.ValidationRunsContainer))
        {
            errors.Add("CosmosRegistry:ValidationRunsContainer must be set.");
        }
        if (options.TombstoneTtlSeconds < 1)
        {
            errors.Add("CosmosRegistry:TombstoneTtlSeconds must be ≥ 1.");
        }

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}
