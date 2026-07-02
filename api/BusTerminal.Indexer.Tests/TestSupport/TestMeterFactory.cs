using System.Diagnostics.Metrics;

namespace BusTerminal.Indexer.Tests.TestSupport;

// Minimal IMeterFactory for tests — creates real Meters so a MeterListener can
// observe them, and disposes them on teardown.
internal sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = new();

    public Meter Create(MeterOptions options)
    {
        var m = new Meter(options.Name, options.Version);
        _meters.Add(m);
        return m;
    }

    public void Dispose()
    {
        foreach (var m in _meters) m.Dispose();
    }
}
