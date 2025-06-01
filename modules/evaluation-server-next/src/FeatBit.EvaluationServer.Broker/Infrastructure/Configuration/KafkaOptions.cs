namespace FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;

public class KafkaOptions
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public bool EnableAutoCommit { get; set; } = true;
    public int AutoCommitIntervalMs { get; set; } = 5000;
    public string AutoOffsetReset { get; set; } = "latest";
    public int SessionTimeoutMs { get; set; } = 10000;
} 