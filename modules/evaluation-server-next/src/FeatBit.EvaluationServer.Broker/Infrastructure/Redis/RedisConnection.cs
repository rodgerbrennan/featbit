using FeatBit.EvaluationServer.Broker.Domain.Brokers;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FeatBit.EvaluationServer.Broker.Infrastructure.Redis;

public class RedisConnection : IBrokerConnection
{
    private readonly ConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;

    public RedisConnection(IOptions<RedisOptions> options)
    {
        _connection = ConnectionMultiplexer.Connect(options.Value.ConnectionString);
        _database = _connection.GetDatabase();
        _subscriber = _connection.GetSubscriber();
    }

    public bool IsConnected => _connection.IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Connection is established in constructor
        return Task.CompletedTask;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await _database.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public IDatabase GetDatabase() => _database;
    public ISubscriber GetSubscriber() => _subscriber;

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
} 