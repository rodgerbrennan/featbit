using FeatBit.EvaluationServer.Shared.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FeatBit.EvaluationServer.Broker.Redis;

public sealed partial class RedisMessageConsumer : BackgroundService, IMessageConsumer
{
    private readonly IRedisClient _redisClient;
    private readonly Dictionary<string, IMessageHandler> _handlers;
    private readonly ILogger<RedisMessageConsumer> _logger;

    public RedisMessageConsumer(
        IRedisClient redisClient,
        IEnumerable<IMessageHandler> handlers,
        ILogger<RedisMessageConsumer> logger)
    {
        _redisClient = redisClient;
        _handlers = handlers.ToDictionary(x => x.Topic, x => x);
        _logger = logger;
    }

    public async Task SubscribeAsync(string channel, Func<string, Task> handler)
    {
        var subscriber = _redisClient.GetSubscriber();
        await subscriber.SubscribeAsync(RedisChannel.Literal(channel), async (_, message) =>
        {
            if (message.IsNullOrEmpty)
            {
                return;
            }

            await handler(message.ToString());
        });
    }

    public async Task UnsubscribeAsync(string channel)
    {
        var subscriber = _redisClient.GetSubscriber();
        await subscriber.UnsubscribeAsync(RedisChannel.Literal(channel));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to all topics
        var topics = _handlers.Keys.ToArray();
        var channel = RedisChannel.Pattern(string.Join(",", topics));
        var queue = await _redisClient.GetSubscriber().SubscribeAsync(channel);

        _logger.LogInformation(
            "Start consuming messages through channel {Channel}",
            channel.ToString()
        );

        // Process messages sequentially
        // ref: https://stackexchange.github.io/StackExchange.Redis/PubSubOrder.html
        queue.OnMessage(HandleMessageAsync);
        return;

        async Task HandleMessageAsync(ChannelMessage channelMessage)
        {
            var message = string.Empty;

            try
            {
                var theChannel = channelMessage.Channel;
                if (theChannel.IsNullOrEmpty)
                {
                    return;
                }

                var topic = theChannel.ToString();
                if (!_handlers.TryGetValue(topic, out var handler))
                {
                    _logger.LogWarning("No message handler for topic: {Topic}", topic);
                    return;
                }

                var value = channelMessage.Message;
                if (value.IsNullOrEmpty)
                {
                    return;
                }

                message = value.ToString();
                await handler.HandleAsync(message, stoppingToken);

                _logger.LogDebug("Message {Message} was handled successfully", message);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while consuming message: {Message}", message);
            }
        }
    }
} 