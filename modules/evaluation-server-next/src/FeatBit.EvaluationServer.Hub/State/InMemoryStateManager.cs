using System.Collections.Concurrent;
using FeatBit.EvaluationServer.Hub.Domain;

namespace FeatBit.EvaluationServer.Hub.State;

public class InMemoryStateManager : IStateManager
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Flag>> _flags = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Target>> _targets = new();
    private readonly HashSet<Guid> _envs = new();
    private readonly object _envLock = new();

    public Flag? GetFlag(Guid envId, string flagKey)
    {
        if (_flags.TryGetValue(envId, out var envFlags))
        {
            envFlags.TryGetValue(flagKey, out var flag);
            return flag;
        }

        return null;
    }

    public void UpsertFlag(Flag flag)
    {
        var envFlags = _flags.GetOrAdd(flag.EnvId, _ => new ConcurrentDictionary<string, Flag>());
        envFlags[flag.Key] = flag;
    }

    public void DeleteFlag(Guid envId, string flagKey)
    {
        if (_flags.TryGetValue(envId, out var envFlags))
        {
            envFlags.TryRemove(flagKey, out _);
        }
    }

    public Target? GetTarget(Guid envId, string targetKey)
    {
        if (_targets.TryGetValue(envId, out var envTargets))
        {
            envTargets.TryGetValue(targetKey, out var target);
            return target;
        }

        return null;
    }

    public void UpsertTarget(Guid envId, Target target)
    {
        var envTargets = _targets.GetOrAdd(envId, _ => new ConcurrentDictionary<string, Target>());
        envTargets[target.Key] = target;
    }

    public void DeleteTarget(Guid envId, string targetKey)
    {
        if (_targets.TryGetValue(envId, out var envTargets))
        {
            envTargets.TryRemove(targetKey, out _);
        }
    }

    public bool IsValidEnv(Guid envId)
    {
        lock (_envLock)
        {
            return _envs.Contains(envId);
        }
    }

    public void AddEnv(Guid envId)
    {
        lock (_envLock)
        {
            _envs.Add(envId);
        }
    }

    public void RemoveEnv(Guid envId)
    {
        lock (_envLock)
        {
            _envs.Remove(envId);
            _flags.TryRemove(envId, out _);
            _targets.TryRemove(envId, out _);
        }
    }
} 