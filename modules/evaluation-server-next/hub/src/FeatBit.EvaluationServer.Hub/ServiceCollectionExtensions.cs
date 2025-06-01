using FeatBit.EvaluationServer.Hub.Evaluation;
using FeatBit.EvaluationServer.Hub.State;
using Microsoft.Extensions.DependencyInjection;

namespace FeatBit.EvaluationServer.Hub;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHubServices(this IServiceCollection services)
    {
        // Add domain services
        services.AddSingleton<IStateManager, InMemoryStateManager>();
        services.AddSingleton<IFlagEvaluator, FlagEvaluator>();

        // Add evaluation services
        services.AddSingleton<ITargetEvaluator, TargetEvaluator>();
        services.AddSingleton<IRuleEvaluator, RuleEvaluator>();
        services.AddSingleton<IDistributionEvaluator, DistributionEvaluator>();

        return services;
    }
} 