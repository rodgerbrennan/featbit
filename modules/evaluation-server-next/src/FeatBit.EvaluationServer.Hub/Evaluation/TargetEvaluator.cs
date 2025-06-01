namespace FeatBit.EvaluationServer.Hub.Evaluation;

public class TargetEvaluator : ITargetEvaluator
{
    public Task<bool> EvaluateAsync(string targetKey, string userId, IDictionary<string, object> userAttributes)
    {
        // Implementation will be added later
        return Task.FromResult(false);
    }
} 