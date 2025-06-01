using System.Text.Json;
using FeatBit.EvaluationServer.Broker.Domain.Messages;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace FeatBit.EvaluationServer.Broker.Infrastructure.Postgres;

public class PostgresMessageProducer : IMessageProducer
{
    private readonly PostgresConnection _connection;
    private readonly PostgresOptions _options;
    private readonly ILogger<PostgresMessageProducer> _logger;

    public PostgresMessageProducer(
        PostgresConnection connection,
        IOptions<PostgresOptions> options,
        ILogger<PostgresMessageProducer> logger)
    {
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connection.GetDataSource().OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            command.CommandText = $@"
                INSERT INTO {_options.SchemaName}.{_options.MessagesTableName}
                (topic, message_type, payload, timestamp, metadata)
                VALUES (@topic, @messageType, @payload, @timestamp, @metadata)
                RETURNING id;
            ";

            command.Parameters.AddWithValue("topic", message.Topic);
            command.Parameters.AddWithValue("messageType", message.MessageType);
            command.Parameters.AddWithValue("payload", message.Payload);
            command.Parameters.AddWithValue("timestamp", message.Timestamp);
            command.Parameters.AddWithValue("metadata", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(new { }));

            var id = await command.ExecuteScalarAsync(cancellationToken);

            _logger.LogDebug(
                "Published message with ID {MessageId} to topic {Topic}",
                id,
                message.Topic
            );

            // Notify listeners
            command.CommandText = $"NOTIFY {message.Topic}, '{id}'";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error publishing message to topic {Topic}",
                message.Topic
            );
            throw;
        }
    }

    public async Task PublishBatchAsync(IEnumerable<IMessage> messages, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connection.GetDataSource().OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var message in messages)
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    command.CommandText = $@"
                        INSERT INTO {_options.SchemaName}.{_options.MessagesTableName}
                        (topic, message_type, payload, timestamp, metadata)
                        VALUES (@topic, @messageType, @payload, @timestamp, @metadata)
                        RETURNING id;
                    ";

                    command.Parameters.AddWithValue("topic", message.Topic);
                    command.Parameters.AddWithValue("messageType", message.MessageType);
                    command.Parameters.AddWithValue("payload", message.Payload);
                    command.Parameters.AddWithValue("timestamp", message.Timestamp);
                    command.Parameters.AddWithValue("metadata", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(new { }));

                    var id = await command.ExecuteScalarAsync(cancellationToken);

                    // Notify listeners
                    command.CommandText = $"NOTIFY {message.Topic}, '{id}'";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                _logger.LogDebug("Successfully published batch of {Count} messages", messages.Count());
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing batch of messages");
            throw;
        }
    }

    public async Task<bool> ValidateTopicAsync(string topic)
    {
        try
        {
            await using var connection = await _connection.GetDataSource().OpenConnectionAsync();
            await using var command = connection.CreateCommand();

            command.CommandText = $@"
                SELECT EXISTS (
                    SELECT 1 FROM {_options.SchemaName}.{_options.MessagesTableName}
                    WHERE topic = @topic
                    LIMIT 1
                );
            ";
            command.Parameters.AddWithValue("topic", topic);

            return (bool)await command.ExecuteScalarAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating topic {Topic}", topic);
            return false;
        }
    }
} 