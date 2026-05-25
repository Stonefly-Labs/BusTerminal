using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace BusTerminal.Api.Domain.Serialization;

// Spec 004 / research §6. Adapts JsonResourceSerializer's STJ options to the
// Cosmos SDK's CosmosSerializer abstract base. Without this adapter the SDK
// defaults to Newtonsoft.Json (which we disabled at the csproj level — see
// AzureCosmosDisableNewtonsoftJsonCheck).
public sealed class CosmosStjSerializer : CosmosSerializer
{
    private readonly JsonSerializerOptions _options;

    public CosmosStjSerializer(JsonResourceSerializer underlying)
    {
        _options = underlying.Options;
    }

    public override T FromStream<T>(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using (stream)
        {
            // The SDK uses CosmosSerializer for both document payloads and
            // SDK-internal types (FeedResponse wrappers etc.). If T is Stream the
            // SDK is requesting the raw body; pass it through.
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            return JsonSerializer.Deserialize<T>(stream, _options)
                ?? throw new JsonException($"Cosmos response deserialization yielded null for {typeof(T).Name}.");
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, _options);
        stream.Position = 0;
        return stream;
    }
}
