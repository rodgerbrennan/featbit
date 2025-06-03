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
    private readonly bool _enableAutoCommit;
    private bool _isRunning;
    private IConsumer<string, string>? _consumer;

    public KafkaMessageConsumer(
        KafkaConnection connection,
        ILogger<KafkaMessageConsumer> logger)
    {
        _connection = connection;
        _logger = logger;
        _handlers = new ConcurrentDictionary<string, Func<IMessage, Task>>();
        _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        
        // Get auto-commit setting from the consumer's configuration
        var consumer = _connection.GetConsumer();
        _enableAutoCommit = consumer.MemberId != null; // If we have a member ID, we're in a consumer group and auto-commit is enabled
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
            _consumer = _connection.GetConsumer();
            
            // Subscribe to all topics
            if (_handlers.Count > 0)
            {
                _consumer.Subscribe(_handlers.Keys);
            }

            // Start consumer loop for all topics
            var cts = new CancellationTokenSource();
            _cancellationTokens.TryAdd("main", cts);
            _ = ConsumeLoopAsync(cts.Token);

            _isRunning = true;
            _logger.LogInformation("Kafka message consumer started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Kafka message consumer");
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
            // Cancel all consumer loops
            foreach (var cts in _cancellationTokens.Values)
            {
                cts.Cancel();
            }

            // Ensure all topics are properly unsubscribed and consumers closed
            if (_consumer != null)
            {
                _consumer.Unsubscribe();
                await Task.Run(() => 
                {
                    _consumer.Close();
                    _consumer.Dispose();
                });
                _consumer = null;
            }

            _cancellationTokens.Clear();
            _handlers.Clear();
            _isRunning = false;
            
            _logger.LogInformation("Kafka message consumer stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Kafka message consumer");
            throw;
        }
    }

    public async Task SubscribeAsync(string topic, Func<IMessage, Task> handler)
    {
        _handlers.TryAdd(topic, handler);

        if (_isRunning && _consumer != null)
        {
            // Subscribe to the new topic
            _consumer.Subscribe(_handlers.Keys);
        }
    }

    public Task UnsubscribeAsync(string topic)
    {
        if (_handlers.TryRemove(topic, out _) && _isRunning && _consumer != null)
        {
            if (_handlers.Count > 0)
            {
                _consumer.Subscribe(_handlers.Keys);
            }
            else
            {
                _consumer.Unsubscribe();
            }
        }

        return Task.CompletedTask;
    }

    private async Task ConsumeLoopAsync(CancellationToken cancellationToken)
    {
        if (_consumer == null)
        {
            throw new InvalidOperationException("Consumer is not initialized");
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(cancellationToken);
                    if (consumeResult == null)
                    {
                        continue;
                    }

                    if (_handlers.TryGetValue(consumeResult.Topic, out var handler))
                    {
                        try
                        {
                            var message = JsonSerializer.Deserialize<BrokerMessage>(consumeResult.Message.Value);
                            if (message != null)
                            {
                                await handler(message);
                                
                                // Check if auto-commit is disabled
                                if (!_enableAutoCommit)
                                {
                                    _consumer.Commit(consumeResult);
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(
                                ex,
                                "Error deserializing message from topic {Topic}: {Message}",
                                consumeResult.Topic,
                                consumeResult.Message.Value
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Error processing message from topic {Topic}",
                                consumeResult.Topic
                            );
                        }
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(
                        ex,
                        "Error consuming message from topic {Topic}",
                        ex.ConsumerRecord?.Topic ?? "unknown"
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
            _logger.LogError(ex, "Error in consumer loop");
        }
    }
} 