namespace FeatBit.EvaluationServer.Edge.Domain.Common.Messaging;

public interface IMessageHandler
{
    string MessageType { get; }
    Task HandleAsync(byte[] message);
} 