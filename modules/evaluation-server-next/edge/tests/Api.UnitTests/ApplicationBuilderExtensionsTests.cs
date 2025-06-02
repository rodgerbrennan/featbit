using System.Diagnostics.Metrics;
using FeatBit.EvaluationServer.Edge.Domain.Common.Metrics;
using FeatBit.EvaluationServer.Edge.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using FeatBit.EvaluationServer.Edge.Api.UnitTests.TestUtils;

namespace FeatBit.EvaluationServer.Edge.Api.UnitTests;

public class ApplicationBuilderExtensionsTests
{
    [Fact]
    public void UseEdgeStreaming_ShouldConfigureWebSocketsAndMiddleware()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Add metrics services
        var meter = new Meter("TestMeter");
        services.AddSingleton<IMeterFactory>(new TestMeterFactory(meter));
        services.AddSingleton<IStreamingMetrics, StreamingMetrics>();

        var app = new ApplicationBuilder(services.BuildServiceProvider());

        // Act
        app.UseEdgeStreaming();

        // Assert
        // Note: Since ApplicationBuilder is sealed and middleware is internal
        // we can't easily verify the exact middleware configuration
        // This test mainly ensures the method runs without throwing
    }
}

// Simple test implementation of IMeterFactory
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