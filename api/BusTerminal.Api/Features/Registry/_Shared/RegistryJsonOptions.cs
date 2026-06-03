using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / data-model.md §9 "Naming Cross-Reference". The registry slice
// owns its own STJ options because:
//   - Its enums are PascalCase on the wire (per registry-entity.schema.json),
//     distinct from spec-004's lowercase wire form;
//   - It does NOT use the polymorphic ResourceJsonConverter (registry types
//     are not subclasses of Resource);
//   - It does NOT use the ExtensionsJsonConverter (Metadata is opaque
//     JsonElement?, not the typed Extensions dict).
//
// Used by the persistence layer (CosmosRegistryEntityStore / CosmosAuditEventStore)
// and by the API DTO serialization defaults wired in Program.cs (T032).
public static class RegistryJsonOptions
{
    public static JsonSerializerOptions Default { get; } = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        return options;
    }
}
