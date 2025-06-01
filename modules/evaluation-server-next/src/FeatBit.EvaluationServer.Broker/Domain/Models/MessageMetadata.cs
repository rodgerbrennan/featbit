namespace FeatBit.EvaluationServer.Broker.Domain.Models;

public class MessageMetadata
{
    public string Source { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
} 