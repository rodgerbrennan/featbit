using System.Collections.Concurrent;
using System.Text.Json;
using FeatBit.EvaluationServer.Broker.Domain.Messages;
using FeatBit.EvaluationServer.Broker.Domain.Models;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FeatBit.EvaluationServer.Broker.Infrastructure.Postgres;

public class PostgresMessageConsumer : IMessageConsumer
{
    private readonly PostgresConnection _connection;
    private readonly PostgresOptions _options;
    private readonly ILogger<PostgresMessageConsumer> _logger;
    private readonly ConcurrentDictionary<string, Func<IMessage, Task>> _handlers;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens;
    private bool _isRunning;

    public PostgresMessageConsumer(
        PostgresConnection connection,
        IOptions<PostgresOptions> options,
        ILogger<PostgresMessageConsumer> logger)
    {
        _connection = connection;
        _options = options.Value;
        _logger = logger;
        _handlers = new ConcurrentDictionary<string, Func<IMessage, Task>>();
        _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
    }

    public bool IsRunning => _isRunning;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return Task.CompletedTask;
        }

        foreach (var topic in _handlers.Keys)
        {
            var cts = new CancellationTokenSource();
            _cancellationTokens.TryAdd(topic, cts);
            
            // Start consumer loop for each topic
            _ = ConsumeLoopAsync(topic, cts.Token);
        }

        _isRunning = true;
        _logger.LogInformation("PostgreSQL message consumer started");
        
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            foreach (var cts in _cancellationTokens.Values)
            {
                cts.Cancel();
            }

            _cancellationTokens.Clear();
            _isRunning = false;
            
            _logger.LogInformation("PostgreSQL message consumer stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping PostgreSQL message consumer");
            throw;
        }
    }

    public Task SubscribeAsync(string topic, Func<IMessage, Task> handler)
    {
        _handlers.TryAdd(topic, handler);

        if (_isRunning)
        {
            var cts = new CancellationTokenSource();
            _cancellationTokens.TryAdd(topic, cts);
            
            // Start consumer loop for the new topic
            _ = ConsumeLoopAsync(topic, cts.Token);
        }

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string topic)
    {
        if (_cancellationTokens.TryRemove(topic, out var cts))
        {
            cts.Cancel();
        }

        _handlers.TryRemove(topic, out _);
        return Task.CompletedTask;
    }

    private async Task ConsumeLoopAsync(string topic, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _connection.GetDataSource().OpenConnectionAsync(cancellationToken);
                
                // Listen for notifications
                await using (var listenCommand = connection.CreateCommand())
                {
                    listenCommand.CommandText = $"LISTEN {topic}";
                    await listenCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                connection.Notification += async (sender, args) =>
                {
                    if (args.Channel == topic && long.TryParse(args.Payload, out var messageId))
                    {
                        await ProcessMessageAsync(topic, messageId, cancellationToken);
                    }
                };

                // Process any messages that were missed while not listening
                await ProcessUnprocessedMessagesAsync(topic, cancellationToken);

                // Wait for notifications
                while (!cancellationToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, do nothing
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in consumer loop for topic {Topic}",
                    topic
                );

                // Wait before retrying
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(_options.PollingIntervalMs, cancellationToken);
                }
            }
        }
    }

    private async Task ProcessMessageAsync(string topic, long messageId, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connection.GetDataSource().OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            command.CommandText = $@"
                SELECT payload, message_type, timestamp, metadata
                FROM {_options.SchemaName}.{_options.MessagesTableName}
                WHERE id = @messageId;
            ";
            command.Parameters.AddWithValue("messageId", messageId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var message = new BrokerMessage
                {
                    Topic = topic,
                    Payload = reader.GetString(0),
                    MessageType = reader.GetString(1),
                    Timestamp = reader.GetDateTime(2),
                    Metadata = JsonSerializer.Deserialize<MessageMetadata>(reader.GetString(3))!
                };

                if (_handlers.TryGetValue(topic, out var handler))
                {
                    await handler(message);
                }

                // Update last processed message ID
                await UpdateLastProcessedIdAsync(topic, messageId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing message {MessageId} from topic {Topic}",
                messageId,
                topic
            );
        }
    }

    private async Task ProcessUnprocessedMessagesAsync(string topic, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connection.GetDataSource().OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            command.CommandText = $@"
                WITH subscription AS (
                    INSERT INTO {_options.SchemaName}.{_options.SubscriptionsTableName} (topic)
                    VALUES (@topic)
                    ON CONFLICT (topic) DO UPDATE SET topic = EXCLUDED.topic
                    RETURNING last_processed_id
                )
                SELECT m.id, m.payload, m.message_type, m.timestamp, m.metadata
                FROM {_options.SchemaName}.{_options.MessagesTableName} m
                CROSS JOIN subscription s
                WHERE m.topic = @topic
                AND m.id > s.last_processed_id
                ORDER BY m.id
                LIMIT @batchSize;
            ";
            command.Parameters.AddWithValue("topic", topic);
            command.Parameters.AddWithValue("batchSize", _options.BatchSize);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var messageId = reader.GetInt64(0);
                var message = new BrokerMessage
                {
                    Topic = topic,
                    Payload = reader.GetString(1),
                    MessageType = reader.GetString(2),
                    Timestamp = reader.GetDateTime(3),
                    Metadata = JsonSerializer.Deserialize<MessageMetadata>(reader.GetString(4))!
                };

                if (_handlers.TryGetValue(topic, out var handler))
                {
                    await handler(message);
                }

                await UpdateLastProcessedIdAsync(topic, messageId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing unprocessed messages for topic {Topic}",
                topic
            );
        }
    }

    private async Task UpdateLastProcessedIdAsync(string topic, long messageId, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connection.GetDataSource().OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            command.CommandText = $@"
                UPDATE {_options.SchemaName}.{_options.SubscriptionsTableName}
                SET last_processed_id = @messageId,
                    updated_at = CURRENT_TIMESTAMP
                WHERE topic = @topic
                AND last_processed_id < @messageId;
            ";
            command.Parameters.AddWithValue("topic", topic);
            command.Parameters.AddWithValue("messageId", messageId);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating last processed ID for topic {Topic}",
                topic
            );
        }
    }
} 