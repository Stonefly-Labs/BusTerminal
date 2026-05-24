using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Infrastructure.Credentials;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 004 / FR-018. AAD-only data plane against deployed Azure; well-known
// emulator key only when Endpoint host is `localhost` (research §2).
//
// The serializer is injected via CosmosClientOptions.Serializer so persistence
// reads/writes use the same STJ options that the export/import serializer uses.
// Newtonsoft.Json is intentionally NOT pulled in
// (`AzureCosmosDisableNewtonsoftJsonCheck` in the csproj documents the trade-off).
public sealed class CosmosClientFactory
{
    private readonly CosmosOptions _options;
    private readonly IAzureCredentialFactory _credentialFactory;
    private readonly CosmosStjSerializer _serializer;
    private readonly string? _userAssignedClientId;

    public CosmosClientFactory(
        IOptions<CosmosOptions> options,
        IAzureCredentialFactory credentialFactory,
        CosmosStjSerializer serializer,
        IConfiguration configuration)
    {
        _options = options.Value;
        _credentialFactory = credentialFactory;
        _serializer = serializer;
        _userAssignedClientId = configuration["AZURE_CLIENT_ID"];
    }

    public CosmosClient Create()
    {
        var clientOptions = new CosmosClientOptions
        {
            Serializer = _serializer,
            ApplicationName = "BusTerminal.Api",
            // Direct mode + TCP is the SDK default for non-emulator endpoints. Leave it.

            // Spec 004 / Constitution §V Operational Excellence. Distributed tracing
            // is OFF by default on stable Cosmos SDK builds; flip it here so the
            // `Azure.Cosmos.Operation` ActivitySource (registered on the OTel tracer
            // provider in OpenTelemetryExtensions) emits spans into the App Insights
            // pipeline.
            CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions
            {
                DisableDistributedTracing = false,
            },
        };

        var endpointUri = new Uri(_options.Endpoint);
        var isLocalEmulator = string.Equals(endpointUri.Host, "localhost", StringComparison.OrdinalIgnoreCase);

        if (isLocalEmulator)
        {
            // The emulator's TLS cert is self-signed; the SDK ships a "limit to emulator"
            // connection mode that skips cert validation when targeting localhost.
            clientOptions.ConnectionMode = ConnectionMode.Gateway;
            clientOptions.HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            });

            return new CosmosClient(
                _options.Endpoint,
                _options.LocalEmulatorKey
                    ?? throw new InvalidOperationException("CosmosOptions.LocalEmulatorKey must be set when targeting the emulator."),
                clientOptions);
        }

        var credential = _credentialFactory.CreateCredential(_userAssignedClientId);
        return new CosmosClient(_options.Endpoint, credential, clientOptions);
    }
}
