namespace FeatBit.EvaluationServer.Edge.Domain.Messages;

public class Message : IMessage
{
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
} 