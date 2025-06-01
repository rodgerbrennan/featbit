using FeatBit.EvaluationServer.Hub.Domain;

namespace FeatBit.EvaluationServer.Hub.State;

public interface IStateManager
{
    // Flag operations
    Flag? GetFlag(Guid envId, string flagKey);
    void UpsertFlag(Flag flag);
    void DeleteFlag(Guid envId, string flagKey);

    // Target operations
    Target? GetTarget(Guid envId, string targetKey);
    void UpsertTarget(Guid envId, Target target);
    void DeleteTarget(Guid envId, string targetKey);

    // Environment operations
    bool IsValidEnv(Guid envId);
    void AddEnv(Guid envId);
    void RemoveEnv(Guid envId);
} 