namespace FeatBit.EvaluationServer.Broker.Domain.Messages;

public interface IMessage
{
    string Topic { get; }
    string MessageType { get; }
    DateTimeOffset Timestamp { get; }
    string Payload { get; }
} 