using FeatBit.EvaluationServer.Broker.Core.Services;
using FeatBit.EvaluationServer.Broker.Domain.Messages;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using FeatBit.EvaluationServer.Broker.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FeatBit.EvaluationServer.Broker.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBrokerServices(this IServiceCollection services, IConfiguration configuration)
    {
        var brokerSection = configuration.GetSection("Broker");
        var brokerType = brokerSection.GetSection("Type").Value?.ToLower() ?? "redis";
        
        
        
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

        services.AddSingleton<IMessageRoutingService, MessageRoutingService>();
        
        return services;
    }

    private static IServiceCollection AddRedisMessageBroker(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisOptions>(options => 
            configuration.GetSection("Broker:Redis").Bind(options));

        services.AddSingleton<RedisConnection>();
        services.AddSingleton<IMessageProducer, RedisMessageProducer>();
        services.AddSingleton<IMessageConsumer, RedisMessageConsumer>();

        return services;
    }

    private static IServiceCollection AddKafkaMessageBroker(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(options => 
            configuration.GetSection("Broker:Kafka").Bind(options));

        // Add Kafka services
        // TODO: Implement Kafka services

        return services;
    }

    private static IServiceCollection AddPostgresMessageBroker(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PostgresOptions>(options => 
            configuration.GetSection("Broker:Postgres").Bind(options));

        // Add Postgres services
        // TODO: Implement Postgres services

        return services;
    }
} 