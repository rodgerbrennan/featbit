using Confluent.Kafka;
using FeatBit.EvaluationServer.Shared.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Broker.Kafka;

public sealed class KafkaMessageConsumer : BackgroundService, IMessageConsumer
{
    private readonly ILogger<KafkaMessageConsumer> _logger;
    private readonly IConsumer<Null, string> _consumer;
    private readonly IEnumerable<IMessageHandler> _messageHandlers;

    public KafkaMessageConsumer(
        ConsumerConfig config,
        ILogger<KafkaMessageConsumer> logger,
        IEnumerable<IMessageHandler> messageHandlers)
    {
        _logger = logger;
        _messageHandlers = messageHandlers;

        config.GroupId = $"evaluation-server-{Guid.NewGuid()}";
        _consumer = new ConsumerBuilder<Null, string>(config).Build();
    }

    public Task SubscribeAsync(string channel, Func<string, Task> handler)
    {
        _consumer.Subscribe(channel);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string channel)
    {
        _consumer.Unsubscribe();
        return Task.CompletedTask;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Factory.StartNew(
            async () => { await StartConsumerLoop(stoppingToken); },
            TaskCreationOptions.LongRunning
        );
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        var topics = _messageHandlers.Select(x => x.Topic).ToArray();

        _consumer.Subscribe(topics);
        _logger.LogInformation(
            "Start consuming messages through topics: {Topics}",
            string.Join(',', topics)
        );

        ConsumeResult<Null, string>? consumeResult = null;
        var message = string.Empty;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                consumeResult = _consumer.Consume(cancellationToken);
                if (consumeResult.IsPartitionEOF)
                {
                    // reached end of topic
                    continue;
                }

                var handler = _messageHandlers.FirstOrDefault(x => x.Topic == consumeResult.Topic);
                if (handler == null)
                {
                    _logger.LogWarning("No handler for topic: {Topic}", consumeResult.Topic);
                    continue;
                }

                message = consumeResult.Message?.Value ?? string.Empty;
                await handler.HandleAsync(message, cancellationToken);

                _logger.LogDebug("Message handled: {Message}", message);
            }
            catch (ConsumeException ex)
            {
                var error = ex.Error.ToString();
                _logger.LogError("Failed to consume message: {Message}, Error: {Error}", message, error);

                if (ex.Error.IsFatal)
                {
                    // https://github.com/edenhill/librdkafka/blob/master/INTRODUCTION.md#fatal-consumer-errors
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming message: {Message}", message);
            }
        }

        try
        {
            _consumer.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing consumer");
        }
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
} 