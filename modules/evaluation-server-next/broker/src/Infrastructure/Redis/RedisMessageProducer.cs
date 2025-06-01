using System.Text.Json;
using FeatBit.EvaluationServer.Broker.Domain.Messages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FeatBit.EvaluationServer.Broker.Infrastructure.Redis;

public class RedisMessageProducer : IMessageProducer
{
    private readonly RedisConnection _connection;
    private readonly ILogger<RedisMessageProducer> _logger;

    public RedisMessageProducer(
        RedisConnection connection,
        ILogger<RedisMessageProducer> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task PublishAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriber = _connection.GetSubscriber();
            var serializedMessage = JsonSerializer.Serialize(message);
            var channel = RedisChannel.Literal(message.Topic);
            
            await subscriber.PublishAsync(channel, serializedMessage);
            
            _logger.LogDebug(
                "Published message to topic {Topic}: {Message}",
                message.Topic,
                serializedMessage
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
        foreach (var message in messages)
        {
            await PublishAsync(message, cancellationToken);
        }
    }

    public Task<bool> ValidateTopicAsync(string topic)
    {
        // Redis doesn't require topic validation
        return Task.FromResult(true);
    }
} 