namespace FeatBit.EvaluationServer.Hub.Evaluation;

public interface IRuleEvaluator
{
    Task<bool> EvaluateAsync(Guid ruleId, IDictionary<string, object> userAttributes);
} 