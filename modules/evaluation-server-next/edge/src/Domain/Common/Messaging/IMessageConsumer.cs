namespace FeatBit.EvaluationServer.Edge.Domain.Common.Messaging;

public interface IMessageConsumer<out T>
{
    IAsyncEnumerable<T> ConsumeAsync();
} 