using System.Diagnostics.Metrics;

namespace Testing;

public sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(options.Name, options.Version);

        _meters.Add(meter);

        return meter;
    }

    public void Dispose()
    {
        foreach (var meter in _meters)
        {
            meter.Dispose();
        }
    }
}
