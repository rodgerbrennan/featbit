using Confluent.Kafka;
using FeatBit.EvaluationServer.Broker.Domain.Brokers;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FeatBit.EvaluationServer.Broker.Infrastructure.Kafka;

public class KafkaConnection : IBrokerConnection
{
    private readonly IOptions<KafkaOptions> _options;
    private readonly ILogger<KafkaConnection> _logger;
    private IProducer<string, string>? _producer;
    private IConsumer<string, string>? _consumer;
    private bool _isConnected;

    public KafkaConnection(
        IOptions<KafkaOptions> options,
        ILogger<KafkaConnection> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsConnected => _isConnected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _options.Value.BootstrapServers,
                ClientId = $"{_options.Value.ClientId}-producer"
            };

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _options.Value.BootstrapServers,
                GroupId = _options.Value.GroupId,
                ClientId = $"{_options.Value.ClientId}-consumer",
                EnableAutoCommit = _options.Value.EnableAutoCommit,
                AutoCommitIntervalMs = _options.Value.AutoCommitIntervalMs,
                AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_options.Value.AutoOffsetReset, true),
                SessionTimeoutMs = _options.Value.SessionTimeoutMs
            };

            _producer = new ProducerBuilder<string, string>(producerConfig).Build();
            _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();

            var adminClient = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = _options.Value.BootstrapServers
            }).Build();

            using (adminClient)
            {
                await Task.Run(() => adminClient.GetMetadata(TimeSpan.FromSeconds(5)), cancellationToken);
            }

            _isConnected = true;
            _logger.LogInformation("Successfully connected to Kafka at {BootstrapServers}", _options.Value.BootstrapServers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Kafka at {BootstrapServers}", _options.Value.BootstrapServers);
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var adminClient = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = _options.Value.BootstrapServers
            }).Build();

            using (adminClient)
            {
                var metadata = await Task.Run(() => adminClient.GetMetadata(TimeSpan.FromSeconds(5)));
                return metadata != null && metadata.Brokers.Count > 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Kafka connection");
            return false;
        }
    }

    public IProducer<string, string> GetProducer()
    {
        if (_producer == null)
        {
            throw new InvalidOperationException("Kafka producer is not initialized. Call ConnectAsync first.");
        }
        return _producer;
    }

    public IConsumer<string, string> GetConsumer()
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
            }
            if (_consumer != null)
            {
                await Task.Run(() => _consumer.Dispose());
            }
            _isConnected = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing Kafka connection");
        }
    }
} 