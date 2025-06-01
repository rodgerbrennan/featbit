namespace FeatBit.EvaluationServer.Hub.Messages;

public class EvaluationResult
{
    public string FlagKey { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public bool Value { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;
} 