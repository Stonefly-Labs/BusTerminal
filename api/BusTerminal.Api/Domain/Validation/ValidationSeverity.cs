using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain.Validation;

// Spec 004 / Q3 / FR-013. Wire form is lowercase (`error`, `warning`, `info`)
// matching validation-result.schema.json. The `None` overall-severity sentinel is
// kept on ValidationResult.OverallSeverity (not a per-finding severity).
[JsonConverter(typeof(JsonStringEnumConverter<ValidationSeverity>))]
public enum ValidationSeverity
{
    Error,
    Warning,
    Info,
}
