namespace FeatBit.EvaluationServer.Edge.Domain.Common.Models;

public class MessageContext
{
    public string Type { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
} 