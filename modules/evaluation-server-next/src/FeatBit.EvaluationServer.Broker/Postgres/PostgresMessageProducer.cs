using System.Text.Json;
using Dapper;
using FeatBit.EvaluationServer.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FeatBit.EvaluationServer.Broker.Postgres;

public sealed class PostgresMessageProducer : IMessageProducer
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresMessageProducer> _logger;

    private const string InsertMessageSql = """
        insert into queue_messages (topic, payload, status)
        values (@Topic, @Payload, 'Notified')
        returning id;
        """;

    private const string NotifyChannelSql = "notify {0}, '{1}';";

    public PostgresMessageProducer(NpgsqlDataSource dataSource, ILogger<PostgresMessageProducer> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task PublishAsync<TMessage>(string topic, TMessage? message) where TMessage : class
    {
        if (message == null)
        {
            return;
        }

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var payload = JsonSerializer.Serialize(message);

                // insert message
                var messageId = await connection.ExecuteScalarAsync<long>(
                    InsertMessageSql,
                    new { Topic = topic, Payload = payload },
                    transaction
                );

                // notify channel
                var channel = Topics.ToChannel(topic);
                var notifySql = string.Format(NotifyChannelSql, channel, messageId);
                await connection.ExecuteAsync(notifySql, transaction: transaction);

                await transaction.CommitAsync();

                _logger.LogDebug("Message published successfully - Topic: {Topic}, Id: {Id}", topic, messageId);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to topic: {Topic}", topic);
        }
    }
}

public static class Topics
{
    public static string ToChannel(string topic) => $"channel_{topic.ToLower()}";
} 