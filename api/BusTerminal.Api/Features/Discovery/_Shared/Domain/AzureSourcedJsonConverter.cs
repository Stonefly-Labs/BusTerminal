using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Discovery.Shared.Domain;

// Spec 009 / T013. The runtime side of AzureSourcedEntity's polymorphic
// surface. The [JsonPolymorphic] + [JsonDerivedType] attributes on the base
// record handle System.Text.Json's discriminator dispatch — this class
// exposes a single accessor for the configured JsonSerializerOptions so
// every store and endpoint shares the same converter set.
//
// Why a separate class: System.Text.Json's polymorphism is opt-in per
// JsonSerializerOptions instance. Stores that build their own options (e.g.,
// Cosmos JsonSerializerOptions used inside CosmosClient setup) need to call
// `AzureSourcedJsonConfig.Configure(options)` to pick up the converter
// settings. Without this seam, each store re-rediscovers which converters
// to wire.
public static class AzureSourcedJsonConfig
{
    public static JsonSerializerOptions CreateOptions(JsonSerializerOptions? template = null)
    {
        var options = template is null
            ? new JsonSerializerOptions(JsonSerializerDefaults.Web)
            : new JsonSerializerOptions(template);
        Configure(options);
        return options;
    }

    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Enum-as-string for every discovery enum. JsonStringEnumConverter
        // is idempotent: registering it twice has no observable effect.
        if (!options.Converters.Any(c => c is JsonStringEnumConverter))
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }

        // AzureSourcedEntity polymorphism is declared via attributes on the
        // base record; no custom JsonConverter needed. PropertyNamingPolicy
        // defaults to CamelCase per JsonSerializerDefaults.Web — which
        // matches the Cosmos document shape from data-model.md §1.1.
        options.PropertyNamingPolicy ??= JsonNamingPolicy.CamelCase;
    }
}
