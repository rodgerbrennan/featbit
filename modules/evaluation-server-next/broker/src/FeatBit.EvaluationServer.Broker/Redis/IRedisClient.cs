using StackExchange.Redis;

namespace FeatBit.EvaluationServer.Broker.Redis;

public interface IRedisClient
{
    IConnectionMultiplexer Connection { get; }

    Task<bool> IsHealthyAsync();

    IDatabase GetDatabase();

    ISubscriber GetSubscriber();
} 