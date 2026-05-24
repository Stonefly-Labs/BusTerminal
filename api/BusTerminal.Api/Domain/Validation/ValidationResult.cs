using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain.Validation;

// Spec 004 / FR-013. Persisted on every resource after each validation pass.
// OverallSeverity is computed from the finding list; `None` indicates no findings.
[JsonConverter(typeof(JsonStringEnumConverter<OverallSeverity>))]
public enum OverallSeverity
{
    None,
    Info,
    Warning,
    Error,
}

public sealed record ValidationResult(
    DateTimeOffset EvaluatedAt,
    IReadOnlyCollection<ValidationFinding> Findings)
{
    public OverallSeverity OverallSeverity { get; } = ComputeOverall(Findings);

    private static OverallSeverity ComputeOverall(IReadOnlyCollection<ValidationFinding> findings)
    {
        var current = OverallSeverity.None;
        foreach (var finding in findings)
        {
            var mapped = finding.Severity switch
            {
                ValidationSeverity.Error => OverallSeverity.Error,
                ValidationSeverity.Warning => OverallSeverity.Warning,
                ValidationSeverity.Info => OverallSeverity.Info,
                _ => OverallSeverity.None,
            };

            if (mapped > current)
            {
                current = mapped;
            }
        }

        return current;
    }

    public bool HasErrors => OverallSeverity == OverallSeverity.Error;

    public static ValidationResult Clean(DateTimeOffset evaluatedAt) => new(evaluatedAt, []);
}
