namespace FeatBit.EvaluationServer.Hub.Domain.Evaluation;

public interface IRuleEvaluator
{
    Task<bool> EvaluateAsync(Guid ruleId, IDictionary<string, object> userAttributes);
} 