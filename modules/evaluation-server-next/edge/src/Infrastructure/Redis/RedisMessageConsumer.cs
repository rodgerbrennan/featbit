using System.Text.Json;
using FeatBit.EvaluationServer.Edge.Domain.Interfaces;
using FeatBit.EvaluationServer.Edge.Domain.Messages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FeatBit.EvaluationServer.Edge.Infrastructure.Redis;

public class RedisMessageConsumer : IHostedService
{
    private readonly RedisConnection _connection;
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<RedisMessageConsumer> _logger;
    private ISubscriber? _subscriber;
    private bool _isRunning;

    public RedisMessageConsumer(
        RedisConnection connection,
        IConnectionManager connectionManager,
        ILogger<RedisMessageConsumer> logger)
    {
        _connection = connection;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            _subscriber = _connection.GetSubscriber();
            await SubscribeToAllChannels();
            _isRunning = true;
            _logger.LogInformation("Redis message consumer started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Redis message consumer");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_isRunning || _subscriber == null)
        {
            return Task.CompletedTask;
        }

        try
        {
            _subscriber.UnsubscribeAll();
            _isRunning = false;
            _logger.LogInformation("Redis message consumer stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Redis message consumer");
        }

        return Task.CompletedTask;
    }

    private async Task SubscribeToAllChannels()
    {
        if (_subscriber == null)
        {
            throw new InvalidOperationException("Redis subscriber is not initialized");
        }

        // Subscribe to all channels (we'll filter messages based on connection mapping)
        await _subscriber.SubscribeAsync("*", async (channel, message) =>
        {
            try
            {
                var messageType = channel.ToString();
                var messageContent = message.ToString();

                if (string.IsNullOrEmpty(messageContent))
                {
                    return;
                }

                var parsedMessage = JsonSerializer.Deserialize<Message>(messageContent);
                if (parsedMessage == null)
                {
                    _logger.LogWarning("Failed to deserialize message from Redis channel {Channel}", messageType);
                    return;
                }

                // Get all active connections
                var connections = _connectionManager.GetAllConnections();

                // Forward message to all connected clients
                foreach (var connection in connections)
                {
                    try
                    {
                        if (connection.WebSocket.State == System.Net.WebSockets.WebSocketState.Open)
                        {
                            var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageContent);
                            await connection.SendAsync(messageBytes, CancellationToken.None);
                            
                            _logger.LogDebug(
                                "Forwarded message of type {MessageType} to connection {ConnectionId}",
                                messageType,
                                connection.Connection.Id
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error forwarding message to connection {ConnectionId}",
                            connection.Connection.Id
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from Redis channel {Channel}", channel);
            }
        });

        _logger.LogInformation("Subscribed to all Redis channels");
    }
} 