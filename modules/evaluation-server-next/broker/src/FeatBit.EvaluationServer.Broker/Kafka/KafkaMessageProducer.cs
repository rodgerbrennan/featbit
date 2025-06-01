using System.Net;
using System.Text.Json;
using Confluent.Kafka;
using FeatBit.EvaluationServer.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Broker.Kafka;

public sealed class KafkaMessageProducer : IMessageProducer
{
    private readonly ILogger<KafkaMessageProducer> _logger;
    private readonly IProducer<Null, string> _producer;

    public KafkaMessageProducer(ProducerConfig config, ILogger<KafkaMessageProducer> logger)
    {
        config.ClientId = Dns.GetHostName();

        _producer = new ProducerBuilder<Null, string>(config).Build();
        _logger = logger;
    }

    public Task PublishAsync<TMessage>(string topic, TMessage? message) where TMessage : class
    {
        if (message == null)
        {
            // ignore null message
            return Task.CompletedTask;
        }

        try
        {
            var value = JsonSerializer.Serialize(message);

            // for high throughput processing, we use Produce method, which is also asynchronous, in that it never blocks.
            // https://docs.confluent.io/kafka-clients/dotnet/current/overview.html#producer
            _producer.Produce(topic, new Message<Null, string>
            {
                Value = value
            }, DeliveryHandler);

            void DeliveryHandler(DeliveryReport<Null, string> report)
            {
                if (report.Error.IsError)
                {
                    _logger.LogError("Error delivering message to {Topic}: {Message}, Error: {Error}", 
                        topic, value, report.Error.ToString());
                }
            }
        }
        catch (ProduceException<Null, string> ex)
        {
            var deliveryResult = ex.DeliveryResult;
            _logger.LogError("Error delivering message to {Topic}: {Message}, Error: {Error}", 
                deliveryResult.Topic, deliveryResult.Value, ex.Error.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while publishing message");
        }

        return Task.CompletedTask;
    }
} 