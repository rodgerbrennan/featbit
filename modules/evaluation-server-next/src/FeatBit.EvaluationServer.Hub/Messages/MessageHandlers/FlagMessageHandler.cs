using System.Text.Json;
using FeatBit.EvaluationServer.Hub.Domain;
using FeatBit.EvaluationServer.Hub.State;
using FeatBit.EvaluationServer.Shared.Messages;
using FeatBit.EvaluationServer.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Hub.Messages.MessageHandlers;

public class FlagMessageHandler : IMessageHandler
{
    private readonly IStateManager _stateManager;
    private readonly ILogger<FlagMessageHandler> _logger;

    public string Type => "flag";

    public FlagMessageHandler(IStateManager stateManager, ILogger<FlagMessageHandler> logger)
    {
        _stateManager = stateManager;
        _logger = logger;
    }

    public Task HandleAsync(MessageContext ctx)
    {
        try
        {
            var flag = ctx.Data.Deserialize<Flag>();
            if (flag == null)
            {
                _logger.LogWarning("Invalid flag message: {Message}", ctx.Data.ToString());
                return Task.CompletedTask;
            }

            _stateManager.UpsertFlag(flag);
            _logger.LogDebug("Flag {Key} updated", flag.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling flag message: {Message}", ctx.Data.ToString());
        }

        return Task.CompletedTask;
    }
} 