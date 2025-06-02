using System.Collections.Concurrent;
using FeatBit.EvaluationServer.Hub.Domain.Models;
using FeatBit.EvaluationServer.Hub.Domain.State;

namespace FeatBit.EvaluationServer.Hub.Infrastructure.State;

public class InMemoryStateManager : IStateManager
{
    private readonly ConcurrentDictionary<string, Flag> _flags = new();
    private readonly ConcurrentDictionary<string, Target> _targets = new();
    private readonly ConcurrentDictionary<Guid, Rule> _rules = new();
    private readonly ConcurrentDictionary<Guid, Distribution> _distributions = new();

    public Task<Flag?> GetFlagAsync(Guid envId, string flagKey)
    {
        var key = $"{envId}:{flagKey}";
        _flags.TryGetValue(key, out var flag);
        return Task.FromResult(flag);
    }

    public Task<Target?> GetTargetAsync(string targetKey)
    {
        _targets.TryGetValue(targetKey, out var target);
        return Task.FromResult(target);
    }

    public Task<Rule?> GetRuleAsync(Guid ruleId)
    {
        _rules.TryGetValue(ruleId, out var rule);
        return Task.FromResult(rule);
    }

    public Task<Distribution?> GetDistributionAsync(Guid distributionId)
    {
        _distributions.TryGetValue(distributionId, out var distribution);
        return Task.FromResult(distribution);
    }

    public Task UpdateFlagAsync(Flag flag)
    {
        var key = $"{flag.EnvId}:{flag.Key}";
        _flags.AddOrUpdate(key, flag, (_, _) => flag);
        return Task.CompletedTask;
    }

    public Task UpdateTargetAsync(Target target)
    {
        _targets.AddOrUpdate(target.Key, target, (_, _) => target);
        return Task.CompletedTask;
    }

    public Task UpdateRuleAsync(Rule rule)
    {
        _rules.AddOrUpdate(rule.Id, rule, (_, _) => rule);
        return Task.CompletedTask;
    }

    public Task UpdateDistributionAsync(Distribution distribution)
    {
        _distributions.AddOrUpdate(distribution.Id, distribution, (_, _) => distribution);
        return Task.CompletedTask;
    }

    public Task DeleteFlagAsync(Guid envId, string flagKey)
    {
        var key = $"{envId}:{flagKey}";
        _flags.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task DeleteTargetAsync(string targetKey)
    {
        _targets.TryRemove(targetKey, out _);
        return Task.CompletedTask;
    }

    public Task DeleteRuleAsync(Guid ruleId)
    {
        _rules.TryRemove(ruleId, out _);
        return Task.CompletedTask;
    }

    public Task DeleteDistributionAsync(Guid distributionId)
    {
        _distributions.TryRemove(distributionId, out _);
        return Task.CompletedTask;
    }
} 