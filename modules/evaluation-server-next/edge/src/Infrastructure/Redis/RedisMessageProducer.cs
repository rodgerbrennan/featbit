using System.Text.Json;
using FeatBit.EvaluationServer.Edge.Domain.Messages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FeatBit.EvaluationServer.Edge.Infrastructure.Redis;

public class RedisMessageProducer
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

    public async Task PublishAsync(IMessage message)
    {
        try
        {
            var subscriber = _connection.GetSubscriber();
            var serializedMessage = JsonSerializer.Serialize(message);
            var channel = RedisChannel.Literal(message.Type);
            
            await subscriber.PublishAsync(channel, serializedMessage);
            
            _logger.LogDebug(
                "Published message of type {MessageType}: {Message}",
                message.Type,
                serializedMessage
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error publishing message of type {MessageType}",
                message.Type
            );
            throw;
        }
    }
} 