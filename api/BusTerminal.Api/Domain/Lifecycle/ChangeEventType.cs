using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain.Lifecycle;

// Spec 004 / FR-015 / Q5. Wire form matches change-event.schema.json `eventType` enum.
[JsonConverter(typeof(JsonStringEnumConverter<ChangeEventType>))]
public enum ChangeEventType
{
    Created,
    Updated,
    LifecycleTransitioned,
    SoftDeleted,
    Restored,
}
