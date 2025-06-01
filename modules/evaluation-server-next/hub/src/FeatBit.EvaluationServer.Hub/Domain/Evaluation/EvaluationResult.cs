namespace FeatBit.EvaluationServer.Hub.Domain.Evaluation;

public class EvaluationResult
{
    public string FlagKey { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string VariationId { get; set; } = string.Empty;

    public string VariationName { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public bool TrackEvents { get; set; }

    public bool SendToExperiment { get; set; }

    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;
} 