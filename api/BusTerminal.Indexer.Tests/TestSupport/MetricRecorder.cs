using System.Diagnostics.Metrics;

namespace BusTerminal.Indexer.Tests.TestSupport;

// Captures measurements for a single meter via MeterListener so tests can
// assert on OTel instrument values + tags without a metrics SDK package.
internal sealed class MetricRecorder : IDisposable
{
    public sealed record Measurement(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags);

    private readonly MeterListener _listener = new();
    private readonly List<Measurement> _measurements = new();
    private readonly object _gate = new();

    public MetricRecorder(string meterName)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == meterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((inst, val, tags, _) => Record(inst, val, tags));
        _listener.SetMeasurementEventCallback<int>((inst, val, tags, _) => Record(inst, val, tags));
        _listener.SetMeasurementEventCallback<double>((inst, val, tags, _) => Record(inst, val, tags));
        _listener.Start();
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var t in tags) dict[t.Key] = t.Value;
        lock (_gate) _measurements.Add(new Measurement(instrument.Name, value, dict));
    }

    public IReadOnlyList<Measurement> Measurements(string instrument)
    {
        lock (_gate) return _measurements.Where(m => m.Instrument == instrument).ToList();
    }

    public IReadOnlyList<double> Values(string instrument) =>
        Measurements(instrument).Select(m => m.Value).ToList();

    private IEnumerable<Measurement> ForTag(string instrument, string tagKey, string tagValue) =>
        Measurements(instrument).Where(m =>
            m.Tags.TryGetValue(tagKey, out var v) && (string?)v == tagValue);

    public double Sum(string instrument, string tagKey, string tagValue) =>
        ForTag(instrument, tagKey, tagValue).Sum(m => m.Value);

    public int Count(string instrument, string tagKey, string tagValue) =>
        ForTag(instrument, tagKey, tagValue).Count();

    public void Dispose() => _listener.Dispose();
}
