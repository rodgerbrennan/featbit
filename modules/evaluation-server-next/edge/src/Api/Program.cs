using FeatBit.EvaluationServer.Edge.WebSocket;
using FeatBit.EvaluationServer.Edge.WebSocket.Connections;
using FeatBit.EvaluationServer.Edge.Metrics;
using FeatBit.EvaluationServer.Hub;
using FeatBit.EvaluationServer.Shared.Metrics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.Configure<StreamingOptions>(
    builder.Configuration.GetSection(nameof(StreamingOptions))
);

// Add health checks
builder.Services.AddHealthChecks();

// Add connection management
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

// Add Hub services
builder.Services.AddHubServices();

// Add metrics
builder.Services.AddSingleton<IStreamingMetrics, StreamingMetrics>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Map health check endpoints
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
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
app.UseStreaming();

app.Run(); 