using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using BusTerminal.Api.Features.Discovery.Shared.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Features.Discovery.Shared;

// Spec 009 / T042 + R-13. Publishes the minimal envelope to the internal
// `discovery-requested` Service Bus queue so the indexer worker can drain
// it asynchronously. Uses AAD-only authentication via the workload UAMI
// (`__fullyQualifiedNamespace` config — no SAS / connection strings).
//
// Trace propagation: when an Activity is active on the sender, the Service
// Bus SDK stamps the message's `Diagnostic-Id` application property with the
// current W3C traceparent. The worker function binding (T054) seeds its root
// Activity from that property so spans correlate end-to-end.
public interface IDiscoveryRequestPublisher
{
    Task PublishAsync(DiscoveryRequestEnvelope envelope, CancellationToken cancellationToken);
}

// Spec 009 / R-13. Tiny payload — the worker reads the full DiscoveryRun
// document from Cosmos using the runId. Keeping the envelope minimal avoids
// payload/state drift if the run record is updated between send + receive.
public sealed record DiscoveryRequestEnvelope(
    string DiscoveryRunId,
    string NamespaceId,
    string RequestedBy,
    DateTimeOffset RequestedUtc,
    string CorrelationId)
{
    public const string CurrentSchemaVersion = "1.0";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
}

// Options bound from the standard `ServiceBus` config section (matches the
// Functions worker binding convention so a single config block fuels both
// send + receive sides).
public sealed class DiscoveryServiceBusOptions
{
    public const string SectionName = "Discovery:ServiceBus";

    // Fully qualified namespace, e.g. `bt-int-dev.servicebus.windows.net`.
    public string FullyQualifiedNamespace { get; set; } = string.Empty;

    // Queue name — matches the IaC-provisioned queue in
    // `iac/modules/service-bus`.
    public string QueueName { get; set; } = "discovery-requested";
}

public sealed partial class ServiceBusDiscoveryRequestPublisher : IDiscoveryRequestPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusDiscoveryRequestPublisher> _logger;

    public ServiceBusDiscoveryRequestPublisher(
        TokenCredential credential,
        IOptions<DiscoveryServiceBusOptions> options,
        ILogger<ServiceBusDiscoveryRequestPublisher> logger)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.FullyQualifiedNamespace))
        {
            throw new InvalidOperationException(
                "Discovery:ServiceBus:FullyQualifiedNamespace must be configured.");
        }
        _client = new ServiceBusClient(opts.FullyQualifiedNamespace, credential);
        _sender = _client.CreateSender(opts.QueueName);
        _logger = logger;
    }

    public async Task PublishAsync(DiscoveryRequestEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        var message = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            MessageId = envelope.DiscoveryRunId,
            CorrelationId = envelope.CorrelationId,
        };
        // R-13 — `Diagnostic-Id` is the property the Service Bus SDK uses for
        // W3C trace propagation. We set it explicitly so the contract holds
        // even if a future SDK release changes its default propagation rules.
        var traceparent = Activity.Current?.Id ?? envelope.CorrelationId;
        message.ApplicationProperties["Diagnostic-Id"] = traceparent;

        using var sendActivity = DiscoveryActivitySource.Instance.StartActivity(
            DiscoveryActivitySource.SpanNames.ApiStartDiscovery, ActivityKind.Producer);
        sendActivity?.SetTag(DiscoveryActivitySource.AttributeKeys.RunId, envelope.DiscoveryRunId);
        sendActivity?.SetTag(DiscoveryActivitySource.AttributeKeys.NamespaceId, envelope.NamespaceId);

        await _sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        LogPublished(envelope.DiscoveryRunId, envelope.NamespaceId);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync().ConfigureAwait(false);
        await _client.DisposeAsync().ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 9401, Level = LogLevel.Information,
        Message = "Discovery request enqueued runId={RunId} namespace={NamespaceId}.")]
    private partial void LogPublished(string runId, string namespaceId);
}
