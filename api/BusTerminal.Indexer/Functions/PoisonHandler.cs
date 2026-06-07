using BusTerminal.Indexer.Indexing;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Indexer.Functions;

// Spec 006 / T049 / contracts/indexer-events.md §5. Structured Error-level log
// for permanent failures. The indexer rethrows after logging so the trigger
// continues from the failed offset (standard Functions semantics — no separate
// poison queue in this slice).
public interface IPoisonHandler
{
    void HandlePermanentFailure(
        RegistryEntityChangeFeedItem item,
        string eventType,
        string errorCategory,
        int retryCount,
        Exception cause);
}

public sealed partial class PoisonHandler : IPoisonHandler
{
    private readonly ILogger<PoisonHandler> _logger;

    public PoisonHandler(ILogger<PoisonHandler> logger)
    {
        _logger = logger;
    }

    [LoggerMessage(EventId = 6301, Level = LogLevel.Error,
        Message = "Indexer permanent failure: entityId={EntityId}, eventType={EventType}, errorCategory={ErrorCategory}, retryCount={RetryCount}, correlationId={CorrelationId}.")]
    private partial void LogPermanentFailure(
        Exception cause,
        string entityId,
        string eventType,
        string errorCategory,
        int retryCount,
        string correlationId);

    public void HandlePermanentFailure(
        RegistryEntityChangeFeedItem item,
        string eventType,
        string errorCategory,
        int retryCount,
        Exception cause)
    {
        // CorrelationId per contracts/indexer-events.md §5 — the etag is a
        // useful diagnostic anchor when the trace id is not available; it
        // pairs with the change-feed offset that the host logs separately.
        var correlationId = item.Etag ?? "<unknown>";
        LogPermanentFailure(
            cause,
            item.Id,
            eventType,
            errorCategory,
            retryCount,
            correlationId);
    }
}
