namespace FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;

public class PostgresOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string SchemaName { get; set; } = "public";
    public string MessagesTableName { get; set; } = "messages";
    public string SubscriptionsTableName { get; set; } = "subscriptions";
    public int PollingIntervalMs { get; set; } = 1000;
    public int BatchSize { get; set; } = 100;
    public int RetentionPeriodHours { get; set; } = 24;
} 