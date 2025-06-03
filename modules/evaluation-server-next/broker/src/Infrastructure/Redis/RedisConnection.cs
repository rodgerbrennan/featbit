using FeatBit.EvaluationServer.Broker.Domain.Brokers;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FeatBit.EvaluationServer.Broker.Infrastructure.Redis;

public class RedisConnection : IBrokerConnection
{
    private readonly IOptions<RedisOptions> _options;
    private IConnectionMultiplexer? _connection;
    private bool _isConnected;

    public RedisConnection(IOptions<RedisOptions> options)
    {
        _options = options;
    }

    public bool IsConnected => _isConnected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _connection = await CreateConnectionAsync(_options.Value.ConnectionString, cancellationToken);
        _isConnected = true;
    }

    protected virtual async Task<IConnectionMultiplexer> CreateConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        return await ConnectionMultiplexer.ConnectAsync(connectionString);
    }

    public IDatabase GetDatabase()
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("Redis connection is not initialized. Call ConnectAsync first.");
        }
        return _connection.GetDatabase(_options.Value.Database);
    }

    public ISubscriber GetSubscriber()
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("Redis connection is not initialized. Call ConnectAsync first.");
        }
        return _connection.GetSubscriber();
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (_connection == null)
        {
            return false;
        }

        try
        {
            var db = _connection.GetDatabase(_options.Value.Database);
            await db.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _isConnected = false;
        }
    }
} 