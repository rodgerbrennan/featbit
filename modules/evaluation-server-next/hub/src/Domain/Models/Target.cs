namespace FeatBit.EvaluationServer.Hub.Domain.Models;

public class Target
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Dictionary<string, string> Custom { get; set; } = new();

    public bool Anonymous { get; set; }
} 