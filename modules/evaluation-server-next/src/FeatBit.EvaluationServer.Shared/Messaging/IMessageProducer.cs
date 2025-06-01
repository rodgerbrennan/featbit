namespace FeatBit.EvaluationServer.Shared.Messaging;

public interface IMessageProducer
{
    Task PublishAsync<TMessage>(string channel, TMessage? message) where TMessage : class;
} 