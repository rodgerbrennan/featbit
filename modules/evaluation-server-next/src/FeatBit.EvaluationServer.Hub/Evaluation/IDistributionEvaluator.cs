namespace FeatBit.EvaluationServer.Hub.Evaluation;

public interface IDistributionEvaluator
{
    Task<bool> EvaluateAsync(Guid distributionId, string userId);
} 