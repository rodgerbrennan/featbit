namespace FeatBit.EvaluationServer.Shared.Messaging;

public interface IMessageConsumer
{
    Task SubscribeAsync(string channel, Func<string, Task> handler);
    Task UnsubscribeAsync(string channel);
} 