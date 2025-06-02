namespace FeatBit.EvaluationServer.Edge.Domain.Common.Models;

public class StreamingOptions
{
    public string PathMatch { get; set; } = "/streaming";
    
    public string[] SupportedVersions { get; set; } = ConnectionVersion.All;

    public string[] SupportedTypes { get; set; } = ConnectionType.All;
} 