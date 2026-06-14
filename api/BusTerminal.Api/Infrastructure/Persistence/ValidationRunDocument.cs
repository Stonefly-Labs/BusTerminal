using System.Text.Json.Serialization;
using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 008 / contracts/validation-run.schema.json. Persisted wire shape for
// the `namespace-validation-runs` Cosmos container. Kept as a separate DTO
// from the domain `ValidationRun` record so id types map to strings (Cosmos
// expects string ids) and the Cosmos system fields (_etag, _ts) ride here.
internal sealed record ValidationRunDocument
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("namespaceId")] public required string NamespaceId { get; init; }
    [JsonPropertyName("executedAtUtc")] public required DateTimeOffset ExecutedAtUtc { get; init; }
    [JsonPropertyName("executedBy")] public required string ExecutedBy { get; init; }
    [JsonPropertyName("executedByDisplayNameSnapshot")] public required string ExecutedByDisplayNameSnapshot { get; init; }
    [JsonPropertyName("azureResourceIdAtRun")] public required string AzureResourceIdAtRun { get; init; }
    [JsonPropertyName("aggregateStatus")] public required ValidationStatus AggregateStatus { get; init; }
    [JsonPropertyName("checkResults")] public required IReadOnlyList<ValidationCheckResult> CheckResults { get; init; }
    [JsonPropertyName("armResourceSnapshot")] public ArmResourceSnapshot? ArmResourceSnapshot { get; init; }
    [JsonPropertyName("driftDetected")] public required bool DriftDetected { get; init; }
    [JsonPropertyName("driftFields")] public required IReadOnlyList<DriftField> DriftFields { get; init; }
    [JsonPropertyName("totalDurationMs")] public required int TotalDurationMs { get; init; }
    [JsonPropertyName("_etag")] public string? Etag { get; init; }

    public static ValidationRunDocument FromDomain(ValidationRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new ValidationRunDocument
        {
            Id = run.Id.ToString("D"),
            NamespaceId = run.NamespaceId.ToString("D"),
            ExecutedAtUtc = run.ExecutedAtUtc,
            ExecutedBy = run.ExecutedBy.ToString("D"),
            ExecutedByDisplayNameSnapshot = run.ExecutedByDisplayNameSnapshot,
            AzureResourceIdAtRun = run.AzureResourceIdAtRun,
            AggregateStatus = run.AggregateStatus,
            CheckResults = run.CheckResults,
            ArmResourceSnapshot = run.ArmResourceSnapshot,
            DriftDetected = run.DriftDetected,
            DriftFields = run.DriftFields,
            TotalDurationMs = run.TotalDurationMs,
            Etag = run.Etag,
        };
    }

    public ValidationRun ToDomain(string? etagOverride = null)
    {
        return new ValidationRun(
            Id: Guid.Parse(Id),
            NamespaceId: Guid.Parse(NamespaceId),
            ExecutedAtUtc: ExecutedAtUtc,
            ExecutedBy: Guid.Parse(ExecutedBy),
            ExecutedByDisplayNameSnapshot: ExecutedByDisplayNameSnapshot,
            AzureResourceIdAtRun: AzureResourceIdAtRun,
            AggregateStatus: AggregateStatus,
            CheckResults: CheckResults,
            DriftDetected: DriftDetected,
            DriftFields: DriftFields,
            TotalDurationMs: TotalDurationMs,
            ArmResourceSnapshot: ArmResourceSnapshot,
            Etag: etagOverride ?? Etag);
    }
}
