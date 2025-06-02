using FeatBit.EvaluationServer.Shared.Metrics;

namespace FeatBit.EvaluationServer.Edge.WebSocket;

public class StreamingMetrics : IStreamingMetrics
{
    public void ConnectionEstablished(string type)
    {
        // Implementation will be added later
    }

    public void ConnectionClosed(long durationMs)
    {
        // Implementation will be added later
    }

    public void ConnectionRejected(string reason)
    {
        // Implementation will be added later
    }

    public void ConnectionError(string errorType)
    {
        // Implementation will be added later
    }

    public IDisposable TrackMessageProcessing(string messageType, int messageSizeBytes)
    {
        return new NoOpDisposable();
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
} 