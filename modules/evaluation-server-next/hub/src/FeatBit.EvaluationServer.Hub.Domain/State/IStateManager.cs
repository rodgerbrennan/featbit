using FeatBit.EvaluationServer.Hub.Domain.Models;

namespace FeatBit.EvaluationServer.Hub.Domain.State;

public interface IStateManager
{
    Task<Flag?> GetFlagAsync(Guid envId, string flagKey);
    Task<Target?> GetTargetAsync(string targetKey);
    Task<Rule?> GetRuleAsync(Guid ruleId);
    Task<Distribution?> GetDistributionAsync(Guid distributionId);
    
    Task UpdateFlagAsync(Flag flag);
    Task UpdateTargetAsync(Target target);
    Task UpdateRuleAsync(Rule rule);
    Task UpdateDistributionAsync(Distribution distribution);
    
    Task DeleteFlagAsync(Guid envId, string flagKey);
    Task DeleteTargetAsync(string targetKey);
    Task DeleteRuleAsync(Guid ruleId);
    Task DeleteDistributionAsync(Guid distributionId);
} 