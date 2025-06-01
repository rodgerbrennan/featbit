using FeatBit.EvaluationServer.Broker.Domain.Messages;

namespace FeatBit.EvaluationServer.Broker.Domain.Models;

public class BrokerMessage : IMessage
{
    public string Topic { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Payload { get; set; } = string.Empty;
    public MessageMetadata Metadata { get; set; } = new();
} 