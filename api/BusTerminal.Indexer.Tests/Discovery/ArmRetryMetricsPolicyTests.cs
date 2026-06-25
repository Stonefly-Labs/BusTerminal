using Azure.Core;
using Azure.Core.Pipeline;
using Azure.ResourceManager;
using BusTerminal.Indexer.Discovery.Telemetry;
using BusTerminal.Indexer.Tests.TestSupport;
using FluentAssertions;

namespace BusTerminal.Indexer.Tests.Discovery;

// Issue #118 — the policy counts the ARM SDK's re-attempts as
// discovery.arm.retries. The SDK reuses one HttpMessage across all attempts of
// a request and re-invokes per-retry policies once per attempt, so we drive the
// policy with the same message repeatedly (and distinct messages) to prove the
// per-request attempt counting.
public class ArmRetryMetricsPolicyTests
{
    [Fact]
    public async Task Records_one_retry_for_each_attempt_after_the_first()
    {
        using var meterFactory = new TestMeterFactory();
        var meter = new DiscoveryMeter(meterFactory);
        using var recorder = new MetricRecorder(DiscoveryMeter.Name);

        var policy = new ArmRetryMetricsPolicy(meter);
        var message = CreateMessage();

        // Three attempts of one logical request → first is not a retry, the
        // next two are.
        await InvokeAsync(policy, message);
        await InvokeAsync(policy, message);
        await InvokeAsync(policy, message);

        recorder.Values(DiscoveryMeter.InstrumentArmRetries).Should().HaveCount(2);
        recorder.Values(DiscoveryMeter.InstrumentArmRetries).Sum().Should().Be(2);
    }

    [Fact]
    public async Task First_attempt_of_a_request_is_not_counted_as_a_retry()
    {
        using var meterFactory = new TestMeterFactory();
        var meter = new DiscoveryMeter(meterFactory);
        using var recorder = new MetricRecorder(DiscoveryMeter.Name);

        var policy = new ArmRetryMetricsPolicy(meter);

        // Two different requests, each on their first attempt → no retries.
        await InvokeAsync(policy, CreateMessage());
        await InvokeAsync(policy, CreateMessage());

        recorder.Values(DiscoveryMeter.InstrumentArmRetries).Should().BeEmpty();
    }

    private static ValueTask InvokeAsync(ArmRetryMetricsPolicy policy, HttpMessage message) =>
        policy.ProcessAsync(message, new ReadOnlyMemory<HttpPipelinePolicy>(new HttpPipelinePolicy[] { new NoopTerminalPolicy() }));

    // ArmClientOptions is a concrete ClientOptions, so the builder gives us a
    // real pipeline capable of minting a valid HttpMessage for the test.
    private static HttpMessage CreateMessage() =>
        HttpPipelineBuilder.Build(new ArmClientOptions()).CreateMessage();

    // Terminal stand-in for the transport: returns without calling further, so
    // no network I/O occurs.
    private sealed class NoopTerminalPolicy : HttpPipelinePolicy
    {
        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline) =>
            ValueTask.CompletedTask;

        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
        }
    }
}
