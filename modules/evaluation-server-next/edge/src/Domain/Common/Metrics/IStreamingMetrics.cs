namespace FeatBit.EvaluationServer.Edge.Domain.Common.Metrics;

public interface IStreamingMetrics
{
    void ConnectionEstablished(string type);
    void ConnectionClosed(long durationMs);
    void ConnectionRejected(string reason);
    void ConnectionError(string errorType);
    void IncrementConnectionCount();
    void DecrementConnectionCount();
    void IncrementMessageCount();
    IDisposable TrackMessageProcessing(string messageType, int messageSizeBytes);
} 