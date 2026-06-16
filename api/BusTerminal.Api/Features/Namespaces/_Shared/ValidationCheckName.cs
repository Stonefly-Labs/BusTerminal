using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §2 ValidationCheckName + FR-014. Closed set of
// five named checks. Adding a new check is an additive enum extension plus
// a new check implementation under Features/Namespaces/Validation/Checks/.
[JsonConverter(typeof(JsonStringEnumConverter<ValidationCheckName>))]
public enum ValidationCheckName
{
    Existence,
    Accessibility,
    RequiredPermissions,
    IdentityAuthorization,
    ApiReachability,
}
