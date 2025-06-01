using FeatBit.EvaluationServer.Shared.Models;

namespace FeatBit.EvaluationServer.Shared.Messages;

public interface IMessageHandler
{
    string Type { get; }

    Task HandleAsync(MessageContext ctx);
} 