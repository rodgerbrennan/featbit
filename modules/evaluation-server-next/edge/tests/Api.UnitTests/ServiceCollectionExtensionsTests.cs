using FeatBit.EvaluationServer.Edge.Api;
using FeatBit.EvaluationServer.Edge.Domain.Common.Metrics;
using FeatBit.EvaluationServer.Edge.Domain.Common.Models;
using FeatBit.EvaluationServer.Edge.Domain.Interfaces;
using FeatBit.EvaluationServer.Edge.Infrastructure;
using FeatBit.EvaluationServer.Edge.WebSocket;
using FeatBit.EvaluationServer.Edge.WebSocket.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;
using Xunit;
using FeatBit.EvaluationServer.Edge.Api.UnitTests.TestUtils;

namespace FeatBit.EvaluationServer.Edge.Api.UnitTests;

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
    public void AddEdgeServices_WithCustomPath_ShouldSetPathInOptions()
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