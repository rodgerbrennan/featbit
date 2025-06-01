using System;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Hub.Streaming.Metrics;

public class HubMetrics : IHubMetrics
{
    private readonly ILogger<HubMetrics> _logger;

    public HubMetrics(ILogger<HubMetrics> logger)
    {
        _logger = logger;
    }

    public void RecordInvalidRequest()
    {
        _logger.LogWarning("Invalid request recorded");
    }

    public void RecordError()
    {
        _logger.LogError("Error recorded");
    }

    public void RecordEvaluation(string flagKey, bool value, TimeSpan duration)
    {
        _logger.LogInformation(
            "Flag {FlagKey} evaluated to {Value} in {Duration}ms",
            flagKey,
            value,
            duration.TotalMilliseconds);
    }
} 