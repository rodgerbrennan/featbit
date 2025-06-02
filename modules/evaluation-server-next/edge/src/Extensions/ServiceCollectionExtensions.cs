using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.Metrics;

namespace FeatBit.EvaluationServer.Edge.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMetrics(this IServiceCollection services)
    {
        services.AddSingleton<IMeterFactory>(new MeterFactory());
        return services;
    }
} 