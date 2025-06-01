using System.ComponentModel.DataAnnotations;

namespace FeatBit.EvaluationServer.Broker.Postgres;

public class PostgresOptions
{
    public const string Postgres = "Postgres";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
} 