using Confluent.Kafka;
using FeatBit.EvaluationServer.Broker.Kafka;
using FeatBit.EvaluationServer.Broker.Postgres;
using FeatBit.EvaluationServer.Broker.Redis;
using FeatBit.EvaluationServer.Shared.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FeatBit.EvaluationServer.Broker;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedisMessageBroker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RedisOptions>(
            configuration.GetSection("Broker:Redis")
        );

        services.AddSingleton<IMessageProducer, RedisMessageProducer>();
        services.AddSingleton<RedisMessageConsumer>();
        services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<RedisMessageConsumer>());
        services.AddHostedService(sp => sp.GetRequiredService<RedisMessageConsumer>());

        return services;
    }

    public static IServiceCollection AddKafkaMessageBroker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(
            configuration.GetSection("Broker:Kafka")
        );

        services.AddSingleton<IMessageProducer, KafkaMessageProducer>();
        services.AddSingleton<KafkaMessageConsumer>();
        services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<KafkaMessageConsumer>());
        services.AddHostedService(sp => sp.GetRequiredService<KafkaMessageConsumer>());

        return services;
    }

    public static IServiceCollection AddPostgresMessageBroker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PostgresOptions>(
            configuration.GetSection("Broker:Postgres")
        );

        services.AddSingleton<IMessageProducer, PostgresMessageProducer>();
        services.AddSingleton<PostgresMessageConsumer>();
        services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<PostgresMessageConsumer>());
        services.AddHostedService(sp => sp.GetRequiredService<PostgresMessageConsumer>());

        return services;
    }
} 