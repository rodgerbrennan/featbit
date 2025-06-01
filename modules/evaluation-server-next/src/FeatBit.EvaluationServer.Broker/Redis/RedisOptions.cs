using System.ComponentModel.DataAnnotations;

namespace FeatBit.EvaluationServer.Broker.Redis;

public class RedisOptions
{
    public const string Redis = "Redis";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
} 