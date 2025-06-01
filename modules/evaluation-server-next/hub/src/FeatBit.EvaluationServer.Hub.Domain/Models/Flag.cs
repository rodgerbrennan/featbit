namespace FeatBit.EvaluationServer.Hub.Domain.Models;

public class Flag
{
    public Guid Id { get; set; }
    public Guid EnvId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsArchived { get; set; }
    public List<Rule> Rules { get; set; } = new();
    public Distribution? DefaultDistribution { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
} 