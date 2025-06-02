using FeatBit.EvaluationServer.Hub.Domain.Common.Models;

namespace FeatBit.EvaluationServer.Hub.Domain.Common.Messages;

public interface IMessageHandler
{
    string MessageType { get; }
    Task HandleAsync(ConnectionContext context, ReadOnlyMemory<byte> message);
} 