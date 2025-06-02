using FeatBit.EvaluationServer.Hub.Domain.Evaluation;
using FeatBit.EvaluationServer.Hub.Domain.State;
using FeatBit.EvaluationServer.Hub.Domain.Models;

namespace FeatBit.EvaluationServer.Hub.Infrastructure.Evaluation;

public class FlagEvaluator : IFlagEvaluator
{
    private readonly IStateManager _stateManager;
    private readonly ITargetEvaluator _targetEvaluator;
    private readonly IRuleEvaluator _ruleEvaluator;
    private readonly IDistributionEvaluator _distributionEvaluator;

    public FlagEvaluator(
        IStateManager stateManager,
        ITargetEvaluator targetEvaluator,
        IRuleEvaluator ruleEvaluator,
        IDistributionEvaluator distributionEvaluator)
    {
        _stateManager = stateManager;
        _targetEvaluator = targetEvaluator;
        _ruleEvaluator = ruleEvaluator;
        _distributionEvaluator = distributionEvaluator;
    }

    public async Task<bool> EvaluateAsync(Guid envId, string flagKey, string userId, IDictionary<string, object> userAttributes)
    {
        // Implementation will be added later
        return await Task.FromResult(false);
    }
} 