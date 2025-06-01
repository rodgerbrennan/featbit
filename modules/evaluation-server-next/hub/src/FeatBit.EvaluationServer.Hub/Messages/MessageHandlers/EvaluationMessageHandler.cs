using System.Text.Json;
using FeatBit.EvaluationServer.Hub.Domain;
using FeatBit.EvaluationServer.Hub.Evaluation;
using FeatBit.EvaluationServer.Hub.Messages;
using FeatBit.EvaluationServer.Hub.State;
using FeatBit.EvaluationServer.Shared.Messages;
using FeatBit.EvaluationServer.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Hub.Messages.MessageHandlers;

public class EvaluationMessageHandler : IMessageHandler
{
    private readonly IStateManager _stateManager;
    private readonly IFlagEvaluator _evaluator;
    private readonly IMessageProducer _messageProducer;
    private readonly ILogger<EvaluationMessageHandler> _logger;

    public string Type => "evaluation";

    public EvaluationMessageHandler(
        IStateManager stateManager,
        IFlagEvaluator evaluator,
        IMessageProducer messageProducer,
        ILogger<EvaluationMessageHandler> logger)
    {
        _stateManager = stateManager;
        _evaluator = evaluator;
        _messageProducer = messageProducer;
        _logger = logger;
    }

    public async Task HandleAsync(MessageContext ctx)
    {
        try
        {
            var request = ctx.Data.Deserialize<EvaluationRequest>();
            if (request == null)
            {
                _logger.LogWarning("Invalid evaluation request: {Message}", ctx.Data.ToString());
                return;
            }

            var value = await _evaluator.EvaluateAsync(
                ctx.Connection.EnvId,
                request.FlagKey,
                request.TargetKey,
                ctx.Connection.UserAttributes);

            var result = new EvaluationResult
            {
                FlagKey = request.FlagKey,
                TargetKey = request.TargetKey,
                Value = value
            };

            // Publish evaluation result
            await _messageProducer.PublishAsync($"evaluation-result-{ctx.Connection.EnvId}", result);

            _logger.LogDebug("Flag {Key} evaluated for target {TargetKey}", request.FlagKey, request.TargetKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling evaluation request: {Message}", ctx.Data.ToString());
        }
    }
}

public class EvaluationRequest
{
    public string FlagKey { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
} 