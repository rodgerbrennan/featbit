using System.Text.Json;
using FeatBit.EvaluationServer.Hub.Domain.Evaluation;
using FeatBit.EvaluationServer.Hub.Domain.Models;
using FeatBit.EvaluationServer.Hub.Streaming.Messages;
using FeatBit.EvaluationServer.Hub.Streaming.Metrics;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Hub.Streaming.Messages.MessageHandlers;

public class EvaluationMessageHandler : IMessageHandler
{
    private readonly IFlagEvaluator _evaluator;
    private readonly IHubMetrics _metrics;
    private readonly ILogger<EvaluationMessageHandler> _logger;

    public string MessageType => "evaluation";

    public EvaluationMessageHandler(
        IFlagEvaluator evaluator,
        IHubMetrics metrics,
        ILogger<EvaluationMessageHandler> logger)
    {
        _evaluator = evaluator;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task HandleAsync(ConnectionContext context, byte[] message)
    {
        try
        {
            var request = JsonSerializer.Deserialize<EvaluationRequest>(message);
            if (request == null)
            {
                _logger.LogWarning("Invalid evaluation request: {Message}", System.Text.Encoding.UTF8.GetString(message));
                _metrics.RecordInvalidRequest();
                return;
            }

            var startTime = DateTimeOffset.UtcNow;
            var value = await _evaluator.EvaluateAsync(
                request.EnvId,
                request.FlagKey,
                request.TargetKey,
                request.UserAttributes);

            var result = new EvaluationResult
            {
                FlagKey = request.FlagKey,
                TargetKey = request.TargetKey,
                Value = value,
                EvaluatedAt = DateTimeOffset.UtcNow
            };

            await context.SendAsync($"evaluation-result-{request.EnvId}", result);
            
            _metrics.RecordEvaluation(
                request.FlagKey,
                result.Value,
                DateTimeOffset.UtcNow - startTime);

            _logger.LogDebug(
                "Flag {Key} evaluated for target {TargetKey}",
                request.FlagKey,
                request.TargetKey);
        }
        catch (Exception ex)
        {
            _metrics.RecordError();
            _logger.LogError(
                ex,
                "Error handling evaluation request: {Message}",
                System.Text.Encoding.UTF8.GetString(message));
        }
    }
}

public class EvaluationRequest
{
    public Guid EnvId { get; set; }
    public string FlagKey { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public IDictionary<string, object> UserAttributes { get; set; } = new Dictionary<string, object>();
}

public class EvaluationResult
{
    public string FlagKey { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public bool Value { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
} 