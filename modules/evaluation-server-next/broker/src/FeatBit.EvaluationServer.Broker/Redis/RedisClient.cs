using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FeatBit.EvaluationServer.Broker.Redis;

public class RedisClient : IRedisClient
{
    private readonly Lazy<ConnectionMultiplexer> _lazyConnection;

    public IConnectionMultiplexer Connection => _lazyConnection.Value;

    public RedisClient(IOptions<RedisOptions> options)
    {
        var connectionString = options.Value.ConnectionString;
        var configOptions = ConfigurationOptions.Parse(connectionString);

        // if we specified a password in the configuration, use it
        var password = options.Value.Password;
        if (!string.IsNullOrWhiteSpace(password))
        {
            configOptions.Password = password;
        }

        _lazyConnection = new Lazy<ConnectionMultiplexer>(
            () => ConnectionMultiplexer.Connect(configOptions.ToString(includePassword: true))
        );
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var db = GetDatabase();
            return await db.PingAsync() < TimeSpan.FromSeconds(1);
        }
        catch
        {
            return false;
        }
    }

    public IDatabase GetDatabase() => Connection.GetDatabase();

    public ISubscriber GetSubscriber() => Connection.GetSubscriber();
} 