using System.Text.Json;
using Confluent.Kafka;
using FeatBit.EvaluationServer.Broker.Domain.Messages;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FeatBit.EvaluationServer.Broker.Infrastructure.Kafka;

public class KafkaMessageProducer : IMessageProducer
{
    private readonly KafkaConnection _connection;
    private readonly ILogger<KafkaMessageProducer> _logger;
    private readonly IOptions<KafkaOptions> _options;

    public KafkaMessageProducer(
        KafkaConnection connection,
        ILogger<KafkaMessageProducer> logger,
        IOptions<KafkaOptions> options)
    {
        _connection = connection;
        _logger = logger;
        _options = options;
    }

    public async Task PublishAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var producer = _connection.GetProducer();
            var serializedMessage = JsonSerializer.Serialize(message);
            
            var kafkaMessage = new Message<string, string>
            {
                Key = message.MessageType,
                Value = serializedMessage,
                Timestamp = new Timestamp(message.Timestamp)
            };

            var result = await producer.ProduceAsync(
                message.Topic,
                kafkaMessage,
                cancellationToken
            );

            _logger.LogDebug(
                "Published message to topic {Topic} at partition {Partition}, offset {Offset}",
                result.Topic,
                result.Partition,
                result.Offset
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error publishing message to topic {Topic}",
                message.Topic
            );
            throw;
        }
    }

    public async Task PublishBatchAsync(IEnumerable<IMessage> messages, CancellationToken cancellationToken = default)
    {
        var producer = _connection.GetProducer();
        var tasks = new List<Task<DeliveryResult<string, string>>>();

        foreach (var message in messages)
        {
            try
            {
                var serializedMessage = JsonSerializer.Serialize(message);
                var kafkaMessage = new Message<string, string>
                {
                    Key = message.MessageType,
                    Value = serializedMessage,
                    Timestamp = new Timestamp(message.Timestamp)
                };

                tasks.Add(producer.ProduceAsync(message.Topic, kafkaMessage, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error preparing message for topic {Topic} in batch",
                    message.Topic
                );
                throw;
            }
        }

        try
        {
            await Task.WhenAll(tasks);
            _logger.LogDebug("Successfully published batch of {Count} messages", messages.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing batch of messages");
            throw;
        }
    }

    public async Task<bool> ValidateTopicAsync(string topic)
    {
        try
        {
            var adminClient = CreateAdminClient(new AdminClientConfig
            {
                BootstrapServers = _options.Value.BootstrapServers
            });

            using (adminClient)
            {
                var metadata = await Task.Run(() => adminClient.GetMetadata(topic, TimeSpan.FromSeconds(5)));
                return metadata.Topics.Any(t => t.Topic == topic && t.Error.Code == ErrorCode.NoError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating topic {Topic}", topic);
            return false;
        }
    }

    protected virtual IAdminClient CreateAdminClient(AdminClientConfig config)
    {
        return new AdminClientBuilder(config).Build();
    }
} 