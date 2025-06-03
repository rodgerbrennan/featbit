using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FeatBit.EvaluationServer.Edge.Infrastructure.Redis;

public class RedisConnection
{
    private readonly Lazy<ConnectionMultiplexer> _connection;

    public RedisConnection(IOptions<RedisOptions> options)
    {
        _connection = new Lazy<ConnectionMultiplexer>(() =>
            ConnectionMultiplexer.Connect(options.Value.ConnectionString));
    }

    public IConnectionMultiplexer Connection => _connection.Value;

    public ISubscriber GetSubscriber() => Connection.GetSubscriber();
} 