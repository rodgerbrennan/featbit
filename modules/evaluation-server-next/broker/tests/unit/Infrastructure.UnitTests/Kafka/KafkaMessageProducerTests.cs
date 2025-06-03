using System.Text.Json;
using Confluent.Kafka;
using FeatBit.EvaluationServer.Broker.Domain.Messages;
using FeatBit.EvaluationServer.Broker.Domain.Models;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using FeatBit.EvaluationServer.Broker.Infrastructure.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Infrastructure.UnitTests.Kafka;

public class KafkaMessageProducerTests
{
    private readonly Mock<KafkaConnection> _mockConnection;
    private readonly Mock<ILogger<KafkaMessageProducer>> _mockLogger;
    private readonly Mock<IProducer<string, string>> _mockProducer;
    private readonly Mock<IOptions<KafkaOptions>> _mockOptions;
    private readonly KafkaMessageProducer _producer;
    private readonly BrokerMessage _testMessage;

    public KafkaMessageProducerTests()
    {
        _mockConnection = new Mock<KafkaConnection>(null, null);
        _mockLogger = new Mock<ILogger<KafkaMessageProducer>>();
        _mockProducer = new Mock<IProducer<string, string>>();
        _mockOptions = new Mock<IOptions<KafkaOptions>>();

        _mockConnection.Setup(c => c.GetProducer()).Returns(_mockProducer.Object);
        _mockProducer.Setup(p => p.Name).Returns("test-broker");

        _mockOptions.Setup(o => o.Value).Returns(new KafkaOptions
        {
            BootstrapServers = "localhost:9092"
        });

        _producer = new KafkaMessageProducer(_mockConnection.Object, _mockLogger.Object, _mockOptions.Object);

        _testMessage = new BrokerMessage
        {
            Topic = "test-topic",
            MessageType = "test-type",
            Payload = "test-payload",
            Timestamp = DateTime.UtcNow,
            Metadata = new FeatBit.EvaluationServer.Broker.Domain.Models.MessageMetadata
            {
                Source = "test-source",
                CorrelationId = "test-correlation-id",
                Headers = new Dictionary<string, string>
                {
                    { "key1", "value1" }
                }
            }
        };
    }

    [Fact]
    public async Task PublishAsync_SuccessfullyPublishesMessage()
    {
        // Arrange
        var deliveryResult = new DeliveryResult<string, string>
        {
            Topic = _testMessage.Topic,
            Partition = new Partition(0),
            Offset = new Offset(1),
            Message = new Message<string, string>
            {
                Key = _testMessage.MessageType,
                Value = JsonSerializer.Serialize(_testMessage)
            }
        };

        Message<string, string>? capturedMessage = null;
        string? capturedTopic = null;

        _mockProducer.Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((topic, message, token) =>
            {
                capturedTopic = topic;
                capturedMessage = message;
            })
            .ReturnsAsync(deliveryResult);

        // Act
        await _producer.PublishAsync(_testMessage);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Equal(_testMessage.Topic, capturedTopic);
        Assert.Equal(_testMessage.MessageType, capturedMessage.Key);

        var deserializedMessage = JsonSerializer.Deserialize<BrokerMessage>(capturedMessage.Value);
        Assert.NotNull(deserializedMessage);
        Assert.Equal(_testMessage.Topic, deserializedMessage.Topic);
        Assert.Equal(_testMessage.MessageType, deserializedMessage.MessageType);
        Assert.Equal(_testMessage.Payload, deserializedMessage.Payload);
        Assert.Equal(_testMessage.Metadata.Source, deserializedMessage.Metadata.Source);
        Assert.Equal(_testMessage.Metadata.CorrelationId, deserializedMessage.Metadata.CorrelationId);
        Assert.Equal(_testMessage.Metadata.Headers["key1"], deserializedMessage.Metadata.Headers["key1"]);

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Published message")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenProducerThrows_LogsAndRethrows()
    {
        // Arrange
        var exception = new KafkaException(new Error(ErrorCode.Local_Transport));
        _mockProducer.Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act & Assert
        await Assert.ThrowsAsync<KafkaException>(() => _producer.PublishAsync(_testMessage));

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error publishing message")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishBatchAsync_SuccessfullyPublishesAllMessages()
    {
        // Arrange
        var messages = new[]
        {
            _testMessage,
            new BrokerMessage
            {
                Topic = "test-topic-2",
                MessageType = "test-type-2",
                Payload = "test-payload-2",
                Timestamp = DateTime.UtcNow,
                Metadata = new FeatBit.EvaluationServer.Broker.Domain.Models.MessageMetadata
                {
                    Source = "test-source",
                    CorrelationId = "test-correlation-id-2",
                    Headers = new Dictionary<string, string>
                    {
                        { "key2", "value2" }
                    }
                }
            }
        };

        var capturedMessages = new List<(string Topic, Message<string, string> Message)>();

        _mockProducer.Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((topic, message, token) =>
            {
                capturedMessages.Add((topic, message));
            })
            .ReturnsAsync((string t, Message<string, string> m, CancellationToken c) => new DeliveryResult<string, string>
            {
                Topic = t,
                Message = m
            });

        // Act
        await _producer.PublishBatchAsync(messages);

        // Assert
        Assert.Equal(messages.Length, capturedMessages.Count);

        for (var i = 0; i < messages.Length; i++)
        {
            var originalMessage = messages[i];
            var (topic, message) = capturedMessages[i];

            Assert.Equal(originalMessage.Topic, topic);
            Assert.Equal(originalMessage.MessageType, message.Key);

            var deserializedMessage = JsonSerializer.Deserialize<BrokerMessage>(message.Value);
            Assert.NotNull(deserializedMessage);
            Assert.Equal(originalMessage.Topic, deserializedMessage.Topic);
            Assert.Equal(originalMessage.MessageType, deserializedMessage.MessageType);
            Assert.Equal(originalMessage.Payload, deserializedMessage.Payload);
            Assert.Equal(originalMessage.Metadata.Source, deserializedMessage.Metadata.Source);
            Assert.Equal(originalMessage.Metadata.CorrelationId, deserializedMessage.Metadata.CorrelationId);
            Assert.Equal(originalMessage.Metadata.Headers.First().Value, deserializedMessage.Metadata.Headers.First().Value);
        }

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Published batch")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateTopicAsync_WhenTopicExists_ReturnsTrue()
    {
        // Arrange
        var metadata = new Metadata(
            new List<BrokerMetadata>
            {
                new BrokerMetadata(1, "test-broker", 9092)
            },
            new List<TopicMetadata>
            {
                new TopicMetadata(
                    topic: "test-topic",
                    partitions: new List<PartitionMetadata>
                    {
                        new PartitionMetadata(
                            partitionId: 0,
                            leader: 1,
                            replicas: new[] { 1 },
                            inSyncReplicas: new[] { 1 },
                            error: null)
                    },
                    error: null)
            },
            0,
            "test-broker");

        var mockAdminClient = new Mock<IAdminClient>();
        mockAdminClient.Setup(a => a.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(metadata);

        var mockAdminClientBuilder = new Mock<AdminClientBuilder>();
        mockAdminClientBuilder.Setup(b => b.Build()).Returns(mockAdminClient.Object);

        // Use a custom producer class for testing that returns our mock admin client
        var testProducer = new TestableKafkaMessageProducer(
            _mockConnection.Object,
            _mockLogger.Object,
            _mockOptions.Object,
            mockAdminClient.Object);

        // Act
        var result = await testProducer.ValidateTopicAsync("test-topic");

        // Assert
        Assert.True(result);
        mockAdminClient.Verify(a => a.GetMetadata("test-topic", It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public async Task ValidateTopicAsync_WhenTopicDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var metadata = new Metadata(
            new List<BrokerMetadata>(),
            new List<TopicMetadata>(),
            0,
            "test-broker");

        var mockAdminClient = new Mock<IAdminClient>();
        mockAdminClient.Setup(a => a.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(metadata);

        var testProducer = new TestableKafkaMessageProducer(
            _mockConnection.Object,
            _mockLogger.Object,
            _mockOptions.Object,
            mockAdminClient.Object);

        // Act
        var result = await testProducer.ValidateTopicAsync("test-topic");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateTopicAsync_WhenAdminClientThrows_LogsAndReturnsFalse()
    {
        // Arrange
        var exception = new KafkaException(new Error(ErrorCode.Local_Transport));
        var mockAdminClient = new Mock<IAdminClient>();
        mockAdminClient.Setup(a => a.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Throws(exception);

        var testProducer = new TestableKafkaMessageProducer(
            _mockConnection.Object,
            _mockLogger.Object,
            _mockOptions.Object,
            mockAdminClient.Object);

        // Act
        var result = await testProducer.ValidateTopicAsync("test-topic");

        // Assert
        Assert.False(result);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error validating topic")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

public class TestableKafkaMessageProducer : KafkaMessageProducer
{
    private readonly IAdminClient _adminClient;

    public TestableKafkaMessageProducer(
        KafkaConnection connection,
        ILogger<KafkaMessageProducer> logger,
        IOptions<KafkaOptions> options,
        IAdminClient adminClient)
        : base(connection, logger, options)
    {
        _adminClient = adminClient;
    }

    protected override IAdminClient CreateAdminClient(AdminClientConfig config)
    {
        return _adminClient;
    }
} 