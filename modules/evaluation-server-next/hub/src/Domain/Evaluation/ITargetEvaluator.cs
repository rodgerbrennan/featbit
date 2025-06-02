namespace FeatBit.EvaluationServer.Hub.Domain.Evaluation;

public interface ITargetEvaluator
{
    Task<bool> EvaluateAsync(string targetKey, string userId, IDictionary<string, object> userAttributes);
} 