using System.Text.Json;
using FeatBit.EvaluationServer.Hub.Domain;
using FeatBit.EvaluationServer.Hub.State;
using FeatBit.EvaluationServer.Shared.Messages;
using FeatBit.EvaluationServer.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Hub.Messages.MessageHandlers;

public class TargetMessageHandler : IMessageHandler
{
    private readonly IStateManager _stateManager;
    private readonly ILogger<TargetMessageHandler> _logger;

    public string Type => "target";

    public TargetMessageHandler(IStateManager stateManager, ILogger<TargetMessageHandler> logger)
    {
        _stateManager = stateManager;
        _logger = logger;
    }

    public Task HandleAsync(MessageContext ctx)
    {
        try
        {
            var target = ctx.Data.Deserialize<Target>();
            if (target == null)
            {
                _logger.LogWarning("Invalid target message: {Message}", ctx.Data.ToString());
                return Task.CompletedTask;
            }

            _stateManager.UpsertTarget(ctx.Connection.EnvId, target);
            _logger.LogDebug("Target {Key} updated", target.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling target message: {Message}", ctx.Data.ToString());
        }

        return Task.CompletedTask;
    }
} 