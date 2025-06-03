using FeatBit.EvaluationServer.Edge.Streaming;
using FeatBit.EvaluationServer.Edge.Domain.Interfaces;
using FeatBit.EvaluationServer.Edge.Domain.Common.Models;
using FeatBit.EvaluationServer.Edge.Infrastructure;
using FeatBit.EvaluationServer.Edge.Domain.Common.Metrics;
using FeatBit.EvaluationServer.Edge.WebSocket;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using FeatBit.EvaluationServer.Edge.Api;
using FeatBit.EvaluationServer.Edge.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.Configure<StreamingOptions>(
    builder.Configuration.GetSection(nameof(StreamingOptions))
);

// Add health checks
builder.Services.AddHealthChecks();

// Add connection management
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

// Add metrics
builder.Services.AddSingleton<IStreamingMetrics, StreamingMetrics>();

// Add edge services
builder.Services.AddEdgeServices();

// Add Redis services
builder.Services.AddRedisServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Map health check endpoints
app.MapHealthChecks("/health/readiness", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapHealthChecks("/health/liveness", new HealthCheckOptions
{
    Predicate = _ => false,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

// Use WebSocket streaming
app.UseEdgeStreaming();

app.Run(); 