using System;

namespace FeatBit.EvaluationServer.Hub.Streaming.Metrics;

public interface IHubMetrics
{
    void RecordInvalidRequest();
    void RecordError();
    void RecordEvaluation(string flagKey, bool value, TimeSpan duration);
} 