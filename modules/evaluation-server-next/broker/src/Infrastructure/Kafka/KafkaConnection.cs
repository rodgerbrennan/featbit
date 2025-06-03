using Confluent.Kafka;
using FeatBit.EvaluationServer.Broker.Domain.Brokers;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FeatBit.EvaluationServer.Broker.Infrastructure.Kafka;

public class KafkaConnection : IBrokerConnection
{
    private readonly IOptions<KafkaOptions> _options;
    protected readonly ILogger<KafkaConnection> Logger;
    private IProducer<string, string>? _producer;
    private IConsumer<string, string>? _consumer;
    private bool _isConnected;

    public KafkaConnection(
        IOptions<KafkaOptions> options,
        ILogger<KafkaConnection> logger)
    {
        _options = options;
        Logger = logger;
    }

    public bool IsConnected => _isConnected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var adminClient = CreateAdminClient();
            using (adminClient)
            {
                var metadata = await Task.Run(() => adminClient.GetMetadata(TimeSpan.FromSeconds(5)));
                if (metadata != null && metadata.Brokers.Count > 0)
                {
                    _producer = CreateProducer();
                    _consumer = CreateConsumer();
                    _isConnected = true;
                    Logger.LogInformation("Connected to Kafka");
                }
                else
                {
                    throw new Exception("Failed to connect to Kafka: No brokers available");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to Kafka");
            throw;
        }
    }

    protected virtual IAdminClient CreateAdminClient()
    {
        return new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = _options.Value.BootstrapServers
        }).Build();
    }

    protected virtual IProducer<string, string> CreateProducer()
    {
        return new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _options.Value.BootstrapServers,
            ClientId = _options.Value.ClientId
        }).Build();
    }

    protected virtual IConsumer<string, string> CreateConsumer()
    {
        return new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _options.Value.BootstrapServers,
            GroupId = _options.Value.GroupId,
            ClientId = _options.Value.ClientId,
            EnableAutoCommit = _options.Value.EnableAutoCommit,
            AutoCommitIntervalMs = _options.Value.AutoCommitIntervalMs,
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_options.Value.AutoOffsetReset),
            SessionTimeoutMs = _options.Value.SessionTimeoutMs,
            AllowAutoCreateTopics = _options.Value.AllowAutoCreateTopics
        }).Build();
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var adminClient = CreateAdminClient();
            using (adminClient)
            {
                var metadata = await Task.Run(() => adminClient.GetMetadata(TimeSpan.FromSeconds(5)));
                return metadata != null && metadata.Brokers.Count > 0;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to test Kafka connection");
            return false;
        }
    }

    public virtual IProducer<string, string> GetProducer()
    {
        if (_producer == null)
        {
            throw new InvalidOperationException("Kafka producer is not initialized. Call ConnectAsync first.");
        }
        return _producer;
    }

    public virtual IConsumer<string, string> GetConsumer()
    {
        if (_consumer == null)
        {
            throw new InvalidOperationException("Kafka consumer is not initialized. Call ConnectAsync first.");
        }
        return _consumer;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_producer != null)
            {
                await Task.Run(() => _producer.Dispose());
                _producer = null;
            }
            if (_consumer != null)
            {
                await Task.Run(() => _consumer.Dispose());
                _consumer = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to dispose Kafka connection");
        }
        finally
        {
            _isConnected = false;
        }
    }
} 