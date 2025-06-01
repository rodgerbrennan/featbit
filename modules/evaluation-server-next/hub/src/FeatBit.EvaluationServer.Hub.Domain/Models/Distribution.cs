namespace FeatBit.EvaluationServer.Hub.Domain.Models;

public class Distribution
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
} 