using FeatBit.EvaluationServer.Hub.Domain.Common.Messages;
using FeatBit.EvaluationServer.Hub.Domain.Evaluation;
using FeatBit.EvaluationServer.Hub.Domain.State;
using FeatBit.EvaluationServer.Hub.Infrastructure.Evaluation;
using FeatBit.EvaluationServer.Hub.Infrastructure.State;
using FeatBit.EvaluationServer.Hub.Streaming.Messages.MessageHandlers;
using FeatBit.EvaluationServer.Hub.Streaming.Metrics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddHealthChecks();

// Add domain services
builder.Services.AddSingleton<IStateManager, InMemoryStateManager>();

// Add evaluation services
builder.Services.AddSingleton<IFlagEvaluator, FlagEvaluator>();
builder.Services.AddSingleton<ITargetEvaluator, TargetEvaluator>();
builder.Services.AddSingleton<IRuleEvaluator, RuleEvaluator>();
builder.Services.AddSingleton<IDistributionEvaluator, DistributionEvaluator>();

// Add streaming services
builder.Services.AddSingleton<IHubMetrics, HubMetrics>();
builder.Services.AddSingleton<IMessageHandler, EvaluationMessageHandler>();

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

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
