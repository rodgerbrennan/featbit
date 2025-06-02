using System.Diagnostics.Metrics;

namespace FeatBit.EvaluationServer.Edge.Api.UnitTests.TestUtils;

public class TestMeterFactory : IMeterFactory
{
    private readonly Meter _meter;

    public TestMeterFactory(Meter meter)
    {
        _meter = meter;
    }

    public Meter Create(MeterOptions options)
    {
        return _meter;
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
} 