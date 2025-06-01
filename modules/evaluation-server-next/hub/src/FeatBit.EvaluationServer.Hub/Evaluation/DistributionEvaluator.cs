namespace FeatBit.EvaluationServer.Hub.Evaluation;

public class DistributionEvaluator : IDistributionEvaluator
{
    public Task<bool> EvaluateAsync(Guid distributionId, string userId)
    {
        // Implementation will be added later
        return Task.FromResult(false);
    }
} 