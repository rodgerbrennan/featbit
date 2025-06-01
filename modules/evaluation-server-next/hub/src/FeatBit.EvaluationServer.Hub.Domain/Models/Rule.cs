namespace FeatBit.EvaluationServer.Hub.Domain.Models;

public class Rule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<Condition> Conditions { get; set; } = new();
    public Distribution Distribution { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Condition
{
    public string Attribute { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
} 