namespace FeatBit.EvaluationServer.Broker.Domain.Messages;

public interface IMessageConsumer
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SubscribeAsync(string topic, Func<IMessage, Task> handler);
    Task UnsubscribeAsync(string topic);
    bool IsRunning { get; }
} 