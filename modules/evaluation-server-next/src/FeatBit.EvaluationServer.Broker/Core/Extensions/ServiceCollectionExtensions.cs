using FeatBit.EvaluationServer.Broker.Core.Services;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using FeatBit.EvaluationServer.Broker.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FeatBit.EvaluationServer.Broker.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBrokerServices(this IServiceCollection services, IConfiguration configuration)
    {
        var brokerType = configuration.GetValue<string>("Broker:Type")?.ToLower() ?? "redis";
        
        services.AddSingleton<IMessageRoutingService, MessageRoutingService>();
        
        switch (brokerType)
        {
            case "redis":
                services.AddRedisMessageBroker(configuration);
                break;
            case "kafka":
                services.AddKafkaMessageBroker(configuration);
                break;
            case "postgres":
                services.AddPostgresMessageBroker(configuration);
                break;
            default:
                throw new ArgumentException($"Unsupported broker type: {brokerType}");
        }
        
        return services;
    }

    private static IServiceCollection AddRedisMessageBroker(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisOptions>(
            configuration.GetSection("Broker:Redis")
        );

        services.AddSingleton<RedisConnection>();
        services.AddSingleton<RedisMessageProducer>();
        services.AddSingleton<RedisMessageConsumer>();

        return services;
    }

    private static IServiceCollection AddKafkaMessageBroker(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(
            configuration.GetSection("Broker:Kafka")
        );

        // Add Kafka services
        // TODO: Implement Kafka services

        return services;
    }

    private static IServiceCollection AddPostgresMessageBroker(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PostgresOptions>(
            configuration.GetSection("Broker:Postgres")
        );

        // Add Postgres services
        // TODO: Implement Postgres services

        return services;
    }
} 