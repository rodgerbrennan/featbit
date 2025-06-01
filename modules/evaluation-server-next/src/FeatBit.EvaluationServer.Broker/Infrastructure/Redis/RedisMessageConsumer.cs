using System.Text.Json;
using FeatBit.EvaluationServer.Broker.Domain.Messages;
using FeatBit.EvaluationServer.Broker.Domain.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FeatBit.EvaluationServer.Broker.Infrastructure.Redis;

public class RedisMessageConsumer : IMessageConsumer
{
    private readonly RedisConnection _connection;
    private readonly ILogger<RedisMessageConsumer> _logger;
    private readonly Dictionary<string, Func<IMessage, Task>> _handlers;
    private readonly Dictionary<string, ChannelMessageQueue> _queues;
    private bool _isRunning;

    public RedisMessageConsumer(
        RedisConnection connection,
        ILogger<RedisMessageConsumer> logger)
    {
        _connection = connection;
        _logger = logger;
        _handlers = new Dictionary<string, Func<IMessage, Task>>();
        _queues = new Dictionary<string, ChannelMessageQueue>();
    }

    public bool IsRunning => _isRunning;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            foreach (var topic in _handlers.Keys)
            {
                await SubscribeToTopicAsync(topic);
            }

            _isRunning = true;
            _logger.LogInformation("Redis message consumer started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Redis message consumer");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            foreach (var queue in _queues.Values)
            {
                await queue.UnsubscribeAsync();
            }

            _queues.Clear();
            _isRunning = false;
            _logger.LogInformation("Redis message consumer stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Redis message consumer");
            throw;
        }
    }

    public async Task SubscribeAsync(string topic, Func<IMessage, Task> handler)
    {
        _handlers[topic] = handler;

        if (_isRunning)
        {
            await SubscribeToTopicAsync(topic);
        }
    }

    public async Task UnsubscribeAsync(string topic)
    {
        if (_queues.TryGetValue(topic, out var queue))
        {
            await queue.UnsubscribeAsync();
            _queues.Remove(topic);
        }

        _handlers.Remove(topic);
    }

    private async Task SubscribeToTopicAsync(string topic)
    {
        var subscriber = _connection.GetSubscriber();
        var channel = RedisChannel.Literal(topic);
        var queue = await subscriber.SubscribeAsync(channel);

        queue.OnMessage(async channelMessage =>
        {
            try
            {
                if (_handlers.TryGetValue(topic, out var handler))
                {
                    var message = JsonSerializer.Deserialize<BrokerMessage>(channelMessage.Message);
                    if (message != null)
                    {
                        await handler(message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing message from topic {Topic}: {Message}",
                    topic,
                    channelMessage.Message
                );
            }
        });

        _queues[topic] = queue;
        _logger.LogInformation("Subscribed to topic: {Topic}", topic);
    }
} 