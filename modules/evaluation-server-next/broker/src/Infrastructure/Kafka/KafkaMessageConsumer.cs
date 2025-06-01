using System.Collections.Concurrent;
using System.Text.Json;
using Confluent.Kafka;
using FeatBit.EvaluationServer.Broker.Domain.Messages;
using FeatBit.EvaluationServer.Broker.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Broker.Infrastructure.Kafka;

public class KafkaMessageConsumer : IMessageConsumer
{
    private readonly KafkaConnection _connection;
    private readonly ILogger<KafkaMessageConsumer> _logger;
    private readonly ConcurrentDictionary<string, Func<IMessage, Task>> _handlers;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens;
    private bool _isRunning;

    public KafkaMessageConsumer(
        KafkaConnection connection,
        ILogger<KafkaMessageConsumer> logger)
    {
        _connection = connection;
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
        _logger.LogInformation("Kafka message consumer started");
        
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
            // Cancel all consumer loops
            foreach (var cts in _cancellationTokens.Values)
            {
                cts.Cancel();
            }

            _cancellationTokens.Clear();
            _isRunning = false;
            
            _logger.LogInformation("Kafka message consumer stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Kafka message consumer");
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
        var consumer = _connection.GetConsumer();
        consumer.Subscribe(topic);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(cancellationToken);
                    if (consumeResult == null)
                    {
                        continue;
                    }

                    if (_handlers.TryGetValue(topic, out var handler))
                    {
                        var message = JsonSerializer.Deserialize<BrokerMessage>(consumeResult.Message.Value);
                        if (message != null)
                        {
                            await handler(message);
                            
                            if (!_connection.GetConsumer().Handle.AutoCommitEnabled)
                            {
                                consumer.Commit(consumeResult);
                            }
                        }
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(
                        ex,
                        "Error consuming message from topic {Topic}",
                        topic
                    );
                }
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
        }
        finally
        {
            try
            {
                consumer.Unsubscribe();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error unsubscribing from topic {Topic}",
                    topic
                );
            }
        }
    }
} 