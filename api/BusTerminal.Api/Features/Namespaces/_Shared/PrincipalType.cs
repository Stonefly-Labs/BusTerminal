using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §2 PrincipalType. ServicePrincipal is a documented
// future-extension placeholder (data-model §10 "Open considerations") — not
// added in v1.
[JsonConverter(typeof(JsonStringEnumConverter<PrincipalType>))]
public enum PrincipalType
{
    User,
    Group,
}
