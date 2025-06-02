namespace FeatBit.EvaluationServer.Hub.Streaming.Messages;

public class EvaluationRequest
{
    public string FlagKey { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public IDictionary<string, object> UserAttributes { get; set; } = new Dictionary<string, object>();
} 