namespace FeatBit.EvaluationServer.Hub.Evaluation;

public class RuleEvaluator : IRuleEvaluator
{
    public Task<bool> EvaluateAsync(Guid ruleId, IDictionary<string, object> userAttributes)
    {
        // Implementation will be added later
        return Task.FromResult(false);
    }
} 