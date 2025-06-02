using System.Diagnostics;
using System.Diagnostics.Metrics;
using FeatBit.EvaluationServer.Edge.Domain.Common.Metrics;

namespace FeatBit.EvaluationServer.Edge.WebSocket;

public class StreamingMetrics : IStreamingMetrics, IDisposable
{
    private const string MeterName = "FeatBit.Edge.WebSocket";
    private readonly Meter _meter;
    private readonly Counter<long> _connectionsEstablished;
    private readonly Counter<long> _connectionsClosed;
    private readonly Counter<long> _connectionsRejected;
    private readonly Counter<long> _connectionErrors;
    private readonly Counter<long> _connectionCount;
    private readonly Counter<long> _messageCount;
    private readonly Histogram<double> _connectionDurations;
    private readonly Histogram<double> _messageProcessingDurations;
    private readonly Counter<long> _messageProcessingBytes;

    public StreamingMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        
        _connectionsEstablished = _meter.CreateCounter<long>(
            "websocket_connections_established",
            "connections",
            "Number of WebSocket connections established"
        );

        _connectionsClosed = _meter.CreateCounter<long>(
            "websocket_connections_closed",
            "connections",
            "Number of WebSocket connections closed"
        );

        _connectionsRejected = _meter.CreateCounter<long>(
            "websocket_connections_rejected",
            "connections",
            "Number of WebSocket connections rejected"
        );

        _connectionErrors = _meter.CreateCounter<long>(
            "websocket_connection_errors",
            "errors",
            "Number of WebSocket connection errors"
        );

        _connectionCount = _meter.CreateCounter<long>(
            "websocket_connection_count",
            "connections",
            "Current number of active WebSocket connections"
        );

        _messageCount = _meter.CreateCounter<long>(
            "websocket_message_count",
            "messages",
            "Total number of WebSocket messages processed"
        );

        _connectionDurations = _meter.CreateHistogram<double>(
            "websocket_connection_duration",
            "ms",
            "Duration of WebSocket connections"
        );

        _messageProcessingDurations = _meter.CreateHistogram<double>(
            "websocket_message_processing_duration",
            "ms",
            "Duration of WebSocket message processing"
        );

        _messageProcessingBytes = _meter.CreateCounter<long>(
            "websocket_message_processing_bytes",
            "bytes",
            "Total bytes processed in WebSocket messages"
        );
    }

    public void ConnectionEstablished(string type)
    {
        _connectionsEstablished.Add(1, new KeyValuePair<string, object?>("type", type));
    }

    public void ConnectionClosed(long durationMs)
    {
        _connectionsClosed.Add(1);
        _connectionDurations.Record(durationMs);
    }

    public void ConnectionRejected(string reason)
    {
        _connectionsRejected.Add(1, new KeyValuePair<string, object?>("reason", reason));
    }

    public void ConnectionError(string errorType)
    {
        _connectionErrors.Add(1, new KeyValuePair<string, object?>("error_type", errorType));
    }

    public void IncrementConnectionCount()
    {
        _connectionCount.Add(1);
    }

    public void DecrementConnectionCount()
    {
        _connectionCount.Add(-1);
    }

    public void IncrementMessageCount()
    {
        _messageCount.Add(1);
    }

    public IDisposable TrackMessageProcessing(string messageType, int messageSizeBytes)
    {
        _messageProcessingBytes.Add(messageSizeBytes, new KeyValuePair<string, object?>("type", messageType));
        return new MessageProcessingTimer(this, messageType);
    }

    private class MessageProcessingTimer : IDisposable
    {
        private readonly StreamingMetrics _metrics;
        private readonly string _messageType;
        private readonly long _startTimestamp;

        public MessageProcessingTimer(StreamingMetrics metrics, string messageType)
        {
            _metrics = metrics;
            _messageType = messageType;
            _startTimestamp = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
            _metrics._messageProcessingDurations.Record(
                elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("type", _messageType)
            );
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
} 