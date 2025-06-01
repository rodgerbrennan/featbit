using System.Text.Json.Serialization;

namespace FeatBit.EvaluationServer.Hub.Domain;

public class Flag
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string VariationType { get; set; } = string.Empty;

    public ICollection<Variation> Variations { get; set; } = new List<Variation>();

    public ICollection<Rule> Rules { get; set; } = new List<Rule>();

    public bool IsEnabled { get; set; }

    public bool DisabledVariationEnabled { get; set; }

    public Guid? DisabledVariationId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public Guid EnvId { get; set; }

    public string Version { get; set; } = string.Empty;
}

public class Variation
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public class Rule
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public ICollection<Condition> Conditions { get; set; } = new List<Condition>();

    public ICollection<Distribution> Distributions { get; set; } = new List<Distribution>();
}

public class Condition
{
    public string Property { get; set; } = string.Empty;

    public string Op { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public class Distribution
{
    public Guid VariationId { get; set; }

    public double Percentage { get; set; }

    [JsonIgnore]
    public double RolloutPercentage { get; set; }
} 