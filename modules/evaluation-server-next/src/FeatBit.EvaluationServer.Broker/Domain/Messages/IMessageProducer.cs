namespace FeatBit.EvaluationServer.Broker.Domain.Messages;

public interface IMessageProducer
{
    Task PublishAsync(IMessage message, CancellationToken cancellationToken = default);
    Task PublishBatchAsync(IEnumerable<IMessage> messages, CancellationToken cancellationToken = default);
    Task<bool> ValidateTopicAsync(string topic);
} 