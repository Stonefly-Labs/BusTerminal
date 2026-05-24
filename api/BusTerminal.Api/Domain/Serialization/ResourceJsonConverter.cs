using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain.Serialization;

// Spec 004 / Q4. Polymorphic dispatch over `resourceType`. Unknown discriminators
// fall through to UnknownResource (the materialization placeholder created in
// US1 T071) so additive-evolution can land new resource types in later slices
// without retroactive migration of stored documents.
//
// The converter is constructed at serializer-options assembly time with the
// ResourceTypeRegistry instance and an UnknownResource factory closure (US1
// supplies the real factory; Phase 2 wires a default that throws if no factory
// has been set so the misuse is loud).
public sealed class ResourceJsonConverter : JsonConverter<Resource>
{
    private readonly ResourceTypeRegistry _registry;
    private readonly Func<string, JsonElement, JsonSerializerOptions, Resource> _unknownFactory;

    public ResourceJsonConverter(
        ResourceTypeRegistry registry,
        Func<string, JsonElement, JsonSerializerOptions, Resource> unknownFactory)
    {
        _registry = registry;
        _unknownFactory = unknownFactory;
    }

    public override Resource Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (!root.TryGetProperty("resourceType", out var discriminatorProperty)
            || discriminatorProperty.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Resource document missing required string property 'resourceType'.");
        }

        var discriminator = discriminatorProperty.GetString()!;

        // Spec 004 / T103. Relationship documents are a peer type that lives in the
        // same Cosmos container as Resources. They are NOT Resource subtypes, so the
        // Resource converter cannot materialize them — surface misuse loudly so a
        // caller routing relationship JSON through the wrong path gets a structured
        // failure rather than a confusing UnknownResource fallback.
        if (discriminator == ResourceTypeDiscriminators.Relationship)
        {
            throw new JsonException(
                "Encountered a 'relationship' document while deserializing as Resource. " +
                "Relationships are peer documents — deserialize via JsonSerializer.Deserialize<Relationship>(...) " +
                "or read them through ICanonicalResourceStore's relationship API.");
        }

        if (_registry.TryGetType(discriminator, out var clrType))
        {
            // Deserialize as the concrete type using the same options minus this
            // converter (to avoid recursion).
            var concreteOptions = CreateConcreteOptions(options);
            var resource = (Resource?)root.Deserialize(clrType, concreteOptions)
                ?? throw new JsonException($"Failed to deserialize resource of type '{clrType.FullName}'.");
            return resource;
        }

        // Unknown discriminator — hand the raw element + the active options to the
        // registered factory (factory deserializes base fields with these options).
        var concreteOptionsForUnknown = CreateConcreteOptions(options);
        return _unknownFactory(discriminator, root.Clone(), concreteOptionsForUnknown);
    }

    public override void Write(
        Utf8JsonWriter writer,
        Resource value,
        JsonSerializerOptions options)
    {
        var concreteOptions = CreateConcreteOptions(options);
        JsonSerializer.Serialize(writer, (object)value, value.GetType(), concreteOptions);
    }

    private JsonSerializerOptions CreateConcreteOptions(JsonSerializerOptions options)
    {
        var clone = new JsonSerializerOptions(options);
        for (var i = clone.Converters.Count - 1; i >= 0; i--)
        {
            if (clone.Converters[i] is ResourceJsonConverter)
            {
                clone.Converters.RemoveAt(i);
            }
        }

        return clone;
    }
}
