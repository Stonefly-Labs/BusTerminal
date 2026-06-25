using System.Runtime.CompilerServices;
using Azure.Core;
using Azure.Core.Pipeline;

namespace BusTerminal.Indexer.Discovery.Telemetry;

// Issue #118 — makes discovery.arm.retries a live metric. The ARM SDK owns
// retries opaquely (its internal RetryPolicy handles 429/503/transient), so
// there is no app-level retry loop to count. Instead this PER-RETRY pipeline
// policy observes the SDK's own re-attempts from inside the retry loop.
//
// Azure.Core reuses the SAME HttpMessage instance across every attempt of one
// logical request, and a per-retry policy is re-invoked once per attempt, so a
// ConditionalWeakTable keyed on the message gives a reliable per-request
// attempt counter (entries are collected with the message — no leak). Every
// invocation after the first is a retry; we record one increment each, tagged
// with the prior attempt's status (or "exception") so ARM throttling (429) is
// distinguishable from transient 5xx in the metric.
//
// Observation-only: it does not alter the SDK's retry behavior (unlike
// replacing ArmClientOptions.RetryPolicy would).
internal sealed class ArmRetryMetricsPolicy : HttpPipelinePolicy
{
    private readonly DiscoveryMeter _meter;
    private readonly ConditionalWeakTable<HttpMessage, StrongBox<int>> _attempts = new();

    public ArmRetryMetricsPolicy(DiscoveryMeter meter)
    {
        ArgumentNullException.ThrowIfNull(meter);
        _meter = meter;
    }

    public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        => ProcessCoreAsync(message, pipeline, async: true);

    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        => ProcessCoreAsync(message, pipeline, async: false).GetAwaiter().GetResult();

    private async ValueTask ProcessCoreAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline, bool async)
    {
        var attempt = _attempts.GetOrCreateValue(message);
        if (attempt.Value > 0)
        {
            // This invocation is a retry (attempt index >= 1). On a retry the
            // message still carries the prior attempt's response when the SDK
            // retried on a status code; it's absent when retrying on a thrown
            // transport exception.
            var failureClass = message.HasResponse
                ? message.Response.Status.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "exception";
            _meter.ArmRetries.Add(1, new KeyValuePair<string, object?>(DiscoveryMeter.TagFailureClass, failureClass));
        }
        attempt.Value++;

        if (async)
        {
            await ProcessNextAsync(message, pipeline).ConfigureAwait(false);
        }
        else
        {
            ProcessNext(message, pipeline);
        }
    }
}
