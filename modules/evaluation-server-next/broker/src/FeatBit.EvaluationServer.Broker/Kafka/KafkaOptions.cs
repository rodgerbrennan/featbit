using System.ComponentModel.DataAnnotations;

namespace FeatBit.EvaluationServer.Broker.Kafka;

public class KafkaOptions
{
    public const string Kafka = "Kafka";

    [Required]
    public string BootstrapServers { get; set; } = string.Empty;

    public string SecurityProtocol { get; set; } = string.Empty;

    public string SaslMechanism { get; set; } = string.Empty;

    public string SaslUsername { get; set; } = string.Empty;

    public string SaslPassword { get; set; } = string.Empty;
} 