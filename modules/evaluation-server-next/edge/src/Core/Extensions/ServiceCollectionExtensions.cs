using FeatBit.EvaluationServer.Edge.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FeatBit.EvaluationServer.Edge.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisOptions>(options => 
            configuration.GetSection("Redis").Bind(options));

        services.AddSingleton<RedisConnection>();
        services.AddSingleton<RedisMessageProducer>();
        services.AddHostedService<RedisMessageConsumer>();

        return services;
    }
} 