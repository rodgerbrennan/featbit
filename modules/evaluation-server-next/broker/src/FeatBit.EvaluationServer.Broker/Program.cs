using FeatBit.EvaluationServer.Broker;
using FeatBit.EvaluationServer.Hub;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddHealthChecks();

// Add Hub services
builder.Services.AddHubServices();

// Add broker services based on configuration
var brokerType = builder.Configuration.GetValue<string>("Broker:Type")?.ToLower() ?? "redis";

switch (brokerType)
{
    case "redis":
        builder.Services.AddRedisMessageBroker(builder.Configuration);
        break;
    case "kafka":
        builder.Services.AddKafkaMessageBroker(builder.Configuration);
        break;
    case "postgres":
        builder.Services.AddPostgresMessageBroker(builder.Configuration);
        break;
    default:
        throw new ArgumentException($"Unsupported broker type: {brokerType}");
}

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