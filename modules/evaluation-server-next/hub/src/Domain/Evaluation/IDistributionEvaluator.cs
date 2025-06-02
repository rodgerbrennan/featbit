namespace FeatBit.EvaluationServer.Hub.Domain.Evaluation;

public interface IDistributionEvaluator
{
    Task<bool> EvaluateAsync(Guid distributionId, string userId);
} 