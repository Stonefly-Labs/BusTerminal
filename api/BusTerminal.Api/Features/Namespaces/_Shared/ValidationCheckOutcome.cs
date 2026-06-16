using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §2 ValidationCheckOutcome. `Skipped` is used when
// a prerequisite check failed and re-running the dependent check would be
// meaningless (e.g., Existence failed → RequiredPermissions skipped).
[JsonConverter(typeof(JsonStringEnumConverter<ValidationCheckOutcome>))]
public enum ValidationCheckOutcome
{
    Pass,
    Fail,
    Skipped,
}
