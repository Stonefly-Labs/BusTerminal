using System.Diagnostics;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Indexer.Discovery;

// Spec 009 / T054 + R-13. Service Bus queue trigger for the internal
// `discovery-requested` queue. The binding uses AAD-only mode via the
// `ServiceBus__fullyQualifiedNamespace` config setting — no SAS / connection
// strings (constitution: no embedded credentials).
//
// Trace propagation: the Functions Service Bus binding seeds each invocation's
// Activity from the message's `Diagnostic-Id` application property (set by
// the API-side publisher). When the orchestrator opens its `discovery.run`
// span the parent is the API's `discovery.api.start` span.
public sealed partial class DiscoveryRequestedFunction
{
    private readonly EntityDiscoveryOrchestrator _orchestrator;
    private readonly ILogger<DiscoveryRequestedFunction> _logger;

    public DiscoveryRequestedFunction(
        EntityDiscoveryOrchestrator orchestrator,
        ILogger<DiscoveryRequestedFunction> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [Function("DiscoveryRequested")]
    public async Task RunAsync(
        [ServiceBusTrigger("%Discovery:ServiceBus:QueueName%", Connection = "ServiceBus")]
        string messageBody,
        FunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(messageBody);

        DiscoveryRequestedEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<DiscoveryRequestedEnvelope>(messageBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            LogMalformed(ex.Message);
            throw;
        }
        if (envelope is null
            || string.IsNullOrWhiteSpace(envelope.DiscoveryRunId)
            || string.IsNullOrWhiteSpace(envelope.NamespaceId))
        {
            LogMalformed("missing required fields");
            throw new InvalidOperationException("Discovery message envelope is missing required fields.");
        }

        LogReceived(envelope.DiscoveryRunId, envelope.NamespaceId);

        // The Activity is already set up by the Functions Service Bus binding
        // — we just observe it for the run scope.
        var activity = Activity.Current;
        activity?.SetTag("discovery.run_id", envelope.DiscoveryRunId);
        activity?.SetTag("discovery.namespace_id", envelope.NamespaceId);

        var request = new DiscoveryRunRequest(
            DiscoveryRunId: envelope.DiscoveryRunId,
            NamespaceId: envelope.NamespaceId,
            // Environment defaults to "dev" — the orchestrator's resolver
            // overrides this with the real value from the namespace document.
            Environment: envelope.Environment ?? "dev",
            RequestedBy: envelope.RequestedBy ?? "(unknown)",
            CorrelationId: envelope.CorrelationId ?? activity?.Id ?? string.Empty);

        await _orchestrator.RunAsync(request, context.CancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record DiscoveryRequestedEnvelope(
        string SchemaVersion,
        string DiscoveryRunId,
        string NamespaceId,
        string? Environment,
        string? RequestedBy,
        DateTimeOffset? RequestedUtc,
        string? CorrelationId);

    [LoggerMessage(EventId = 9131, Level = LogLevel.Information,
        Message = "Discovery requested runId={RunId} namespace={NamespaceId}.")]
    private partial void LogReceived(string runId, string namespaceId);

    [LoggerMessage(EventId = 9132, Level = LogLevel.Warning,
        Message = "Malformed discovery message: {Reason}")]
    private partial void LogMalformed(string reason);
}
