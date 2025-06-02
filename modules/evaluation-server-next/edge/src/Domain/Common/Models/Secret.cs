namespace FeatBit.EvaluationServer.Edge.Domain.Common.Models;

public class Secret
{
    public string ProjectKey { get; set; } = string.Empty;
    public string EnvKey { get; set; } = string.Empty;
    public Guid EnvId { get; set; }
} 