namespace FeatBit.EvaluationServer.Edge.Domain.Messages;

public interface IMessage
{
    string Type { get; }
    string Payload { get; }
    DateTimeOffset Timestamp { get; }
} 