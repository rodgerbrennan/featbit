using System.Text.Json;
using FeatBit.EvaluationServer.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Broker.Redis;

public sealed partial class RedisMessageProducer : IMessageProducer
{
    private readonly IRedisClient _redisClient;
    private readonly ILogger<RedisMessageProducer> _logger;

    public RedisMessageProducer(IRedisClient redisClient, ILogger<RedisMessageProducer> logger)
    {
        _redisClient = redisClient;
        _logger = logger;
    }

    public async Task PublishAsync<TMessage>(string topic, TMessage? message) where TMessage : class
    {
        try
        {
            var jsonMessage = JsonSerializer.Serialize(message);

            // RPush json message to topic list
            await _redisClient.GetDatabase().ListRightPushAsync(topic, jsonMessage);

            _logger.LogDebug("Message {Message} was published successfully", jsonMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while publishing message");
        }
    }
} 