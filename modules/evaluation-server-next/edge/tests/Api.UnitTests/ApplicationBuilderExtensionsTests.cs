using FeatBit.EvaluationServer.Edge.Api;
using FeatBit.EvaluationServer.Edge.WebSocket;
using FeatBit.EvaluationServer.Shared.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using FeatBit.EvaluationServer.Edge.Domain.Common.Metrics;

namespace Api.UnitTests;

public class ApplicationBuilderExtensionsTests
{
    [Fact]
    public void UseEdgeStreaming_ConfiguresMiddleware()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IStreamingMetrics, StreamingMetrics>();
        builder.Services.AddEdgeServices();
        var app = builder.Build();

        // Act - this should not throw
        app.UseEdgeStreaming();

        // Assert - verify the WebSocket options are configured
        var options = app.Services.GetService<IOptions<WebSocketOptions>>();
        Assert.NotNull(options);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Value.KeepAliveInterval);
    }

    [Fact]
    public void UseEdgeStreaming_WithCustomPath_ConfiguresCustomPath()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IStreamingMetrics, StreamingMetrics>();
        var customPath = "/custom-streaming";
        builder.Services.AddEdgeServices(options =>
        {
            options.PathMatch = customPath;
        });
        var app = builder.Build();

        // Act - this should not throw
        app.UseEdgeStreaming();

        // Assert - verify the streaming options are configured
        var options = app.Services.GetService<IOptions<StreamingOptions>>();
        Assert.NotNull(options);
        Assert.Equal(customPath, options.Value.PathMatch);
    }
} 