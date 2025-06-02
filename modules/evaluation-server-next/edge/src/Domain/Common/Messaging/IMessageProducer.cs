namespace FeatBit.EvaluationServer.Edge.Domain.Common.Messaging;

public interface IMessageProducer<in T>
{
    Task ProduceAsync(T message);
} 