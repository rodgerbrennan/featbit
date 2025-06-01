using FeatBit.EvaluationServer.Broker.Domain.Messages;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Broker.Core.Services;

public class MessageRoutingService : IMessageRoutingService
{
    private readonly IMessageProducer _producer;
    private readonly IMessageConsumer _consumer;
    private readonly ILogger<MessageRoutingService> _logger;

    public MessageRoutingService(
        IMessageProducer producer,
        IMessageConsumer consumer,
        ILogger<MessageRoutingService> logger)
    {
        _producer = producer;
        _consumer = consumer;
        _logger = logger;
    }

    public async Task RouteMessageAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _producer.PublishAsync(message, cancellationToken);
            _logger.LogDebug(
                "Successfully routed message of type {MessageType} to topic {Topic}",
                message.MessageType,
                message.Topic
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error routing message of type {MessageType} to topic {Topic}",
                message.MessageType,
                message.Topic
            );
            throw;
        }
    }

    public Task<bool> ValidateRouteAsync(string topic)
    {
        return _producer.ValidateTopicAsync(topic);
    }

    public Task SubscribeAsync(string topic, Func<IMessage, Task> handler)
    {
        return _consumer.SubscribeAsync(topic, handler);
    }

    public Task UnsubscribeAsync(string topic)
    {
        return _consumer.UnsubscribeAsync(topic);
    }
} 