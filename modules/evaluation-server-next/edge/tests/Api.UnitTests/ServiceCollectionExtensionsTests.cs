using FeatBit.EvaluationServer.Edge.Api;
using FeatBit.EvaluationServer.Edge.Domain.Interfaces;
using FeatBit.EvaluationServer.Edge.Infrastructure;
using FeatBit.EvaluationServer.Edge.WebSocket;
using FeatBit.EvaluationServer.Edge.WebSocket.Messages;
using FeatBit.EvaluationServer.Shared.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;
using Xunit;
using FeatBit.EvaluationServer.Edge.Domain.Common.Metrics;

namespace Api.UnitTests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEdgeServices_RegistersRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Add metrics services
        var meter = new Meter("TestMeter");
        services.AddSingleton<IMeterFactory>(new TestMeterFactory(meter));
        services.AddSingleton<IStreamingMetrics, StreamingMetrics>();

        // Act
        services.AddEdgeServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Verify core services are registered
        Assert.NotNull(serviceProvider.GetService<IConnectionManager>());
        Assert.NotNull(serviceProvider.GetService<MessageDispatcher>());
        
        // Verify the registered IConnectionManager is of type ConnectionManager
        var connectionManager = serviceProvider.GetService<IConnectionManager>();
        Assert.IsType<ConnectionManager>(connectionManager);
    }

    [Fact]
    public void AddEdgeServices_WithCustomOptions_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Add metrics services
        var meter = new Meter("TestMeter");
        services.AddSingleton<IMeterFactory>(new TestMeterFactory(meter));
        services.AddSingleton<IStreamingMetrics, StreamingMetrics>();
        
        var customPath = "/custom-streaming";

        // Act
        services.AddEdgeServices(options =>
        {
            options.PathMatch = customPath;
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<StreamingOptions>>();
        Assert.NotNull(options);
        Assert.Equal(customPath, options.Value.PathMatch);
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