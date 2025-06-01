namespace FeatBit.EvaluationServer.Hub.Streaming.Messages;

public class EvaluationResult
{
    public string FlagKey { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public bool Value { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;
} 