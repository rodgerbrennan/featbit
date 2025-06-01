namespace FeatBit.EvaluationServer.Hub.Domain.Evaluation;

public interface IFlagEvaluator
{
    Task<bool> EvaluateAsync(Guid envId, string flagKey, string userId, IDictionary<string, object> userAttributes);
} 