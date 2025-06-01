namespace FeatBit.EvaluationServer.Broker.Domain.Brokers;

public interface IBrokerConnection : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    bool IsConnected { get; }
    Task<bool> TestConnectionAsync();
} 