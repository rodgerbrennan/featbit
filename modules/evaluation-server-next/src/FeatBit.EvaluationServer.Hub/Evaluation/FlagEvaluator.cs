namespace FeatBit.EvaluationServer.Hub.Evaluation;

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
        // Get flag from state manager
        var flag = _stateManager.GetFlag(envId, flagKey);
        if (flag == null || !flag.IsEnabled)
        {
            return false;
        }

        // Check if user is in target list
        var isTargeted = await _targetEvaluator.EvaluateAsync(userId, userId, userAttributes);
        if (isTargeted)
        {
            return true;
        }

        // Check rules
        foreach (var rule in flag.Rules.Where(r => r.IsEnabled))
        {
            var ruleMatch = await _ruleEvaluator.EvaluateAsync(rule.Id, userAttributes);
            if (ruleMatch)
            {
                // If rule matches, check distribution
                if (rule.Distribution != null)
                {
                    return await _distributionEvaluator.EvaluateAsync(rule.Distribution.Id, userId);
                }
                return true;
            }
        }

        // If no rules match, use default rule
        if (flag.DefaultRule?.Distribution != null)
        {
            return await _distributionEvaluator.EvaluateAsync(flag.DefaultRule.Distribution.Id, userId);
        }

        // Default to false if no conditions are met
        return false;
    }
} 