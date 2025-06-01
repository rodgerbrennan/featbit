using System.Diagnostics;
using System.Diagnostics.Metrics;
using FeatBit.EvaluationServer.Shared.Metrics;

namespace FeatBit.EvaluationServer.Hub.Metrics;

public class StreamingMetrics : IStreamingMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _connectionEstablishedTotal;
    private readonly Counter<long> _connectionClosedTotal;
    private readonly Counter<long> _connectionRejectedTotal;
    private readonly Counter<long> _connectionErrorTotal;
    private readonly Histogram<double> _messageProcessingDuration;
    private readonly Counter<long> _messageProcessedBytes;

    public StreamingMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("FeatBit.EvaluationServer");

        _connectionEstablishedTotal = _meter.CreateCounter<long>(
            "featbit.connection.established",
            description: "Total number of established connections");

        _connectionClosedTotal = _meter.CreateCounter<long>(
            "featbit.connection.closed",
            description: "Total number of closed connections");

        _connectionRejectedTotal = _meter.CreateCounter<long>(
            "featbit.connection.rejected",
            description: "Total number of rejected connections");

        _connectionErrorTotal = _meter.CreateCounter<long>(
            "featbit.connection.error",
            description: "Total number of connection errors");

        _messageProcessingDuration = _meter.CreateHistogram<double>(
            "featbit.message.processing.duration",
            unit: "s",
            description: "Time spent processing messages");

        _messageProcessedBytes = _meter.CreateCounter<long>(
            "featbit.message.processed.bytes",
            unit: "By",
            description: "Total number of bytes processed");
    }

    public void ConnectionEstablished(string type)
    {
        _connectionEstablishedTotal.Add(1, new KeyValuePair<string, object?>("type", type));
    }

    public void ConnectionClosed(long durationMs)
    {
        _connectionClosedTotal.Add(1);
    }

    public void ConnectionRejected(string reason)
    {
        _connectionRejectedTotal.Add(1, new KeyValuePair<string, object?>("reason", reason));
    }

    public void ConnectionError(string errorType)
    {
        _connectionErrorTotal.Add(1, new KeyValuePair<string, object?>("error_type", errorType));
    }

    public IDisposable TrackMessageProcessing(string messageType, int messageSizeBytes)
    {
        _messageProcessedBytes.Add(messageSizeBytes, new KeyValuePair<string, object?>("type", messageType));
        return new MetricTimer(messageType, _messageProcessingDuration);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private sealed class MetricTimer : IDisposable
    {
        private readonly string _messageType;
        private readonly Histogram<double> _histogram;
        private readonly Stopwatch _stopwatch;

        public MetricTimer(string messageType, Histogram<double> histogram)
        {
            _messageType = messageType;
            _histogram = histogram;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _histogram.Record(_stopwatch.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("type", _messageType));
        }
    }
} 