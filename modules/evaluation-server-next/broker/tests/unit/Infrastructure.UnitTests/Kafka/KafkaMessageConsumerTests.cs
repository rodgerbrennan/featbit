using System.Text.Json;
using Confluent.Kafka;
using FeatBit.EvaluationServer.Broker.Domain.Messages;
using FeatBit.EvaluationServer.Broker.Domain.Models;
using FeatBit.EvaluationServer.Broker.Infrastructure.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Infrastructure.UnitTests.Kafka;

public class KafkaMessageConsumerTests
{
    private const string TestTopic = "test-topic";
    private readonly Mock<KafkaConnection> _mockConnection;
    private readonly Mock<ILogger<KafkaMessageConsumer>> _mockLogger;
    private readonly Mock<IConsumer<string, string>> _mockConsumer;
    private readonly Mock<IProducer<string, string>> _mockProducer;
    private readonly KafkaMessageConsumer _consumer;
    private readonly BrokerMessage _testMessage;
    private readonly Mock<IAdminClient> _mockAdminClient;

    public KafkaMessageConsumerTests()
    {
        _mockConnection = new Mock<KafkaConnection>(null, null);
        _mockLogger = new Mock<ILogger<KafkaMessageConsumer>>();
        _mockConsumer = new Mock<IConsumer<string, string>>();
        _mockProducer = new Mock<IProducer<string, string>>();
        _mockAdminClient = new Mock<IAdminClient>();

        _mockConnection.Setup(c => c.GetConsumer()).Returns(_mockConsumer.Object);
        _mockConnection.Setup(c => c.GetProducer()).Returns(_mockProducer.Object);
        
        // Setup consumer member ID to simulate auto-commit enabled
        _mockConsumer.Setup(c => c.MemberId).Returns("test-member-id");

        _consumer = new KafkaMessageConsumer(_mockConnection.Object, _mockLogger.Object);

        _testMessage = new BrokerMessage
        {
            Topic = TestTopic,
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
    public async Task StartAsync_WhenNotRunning_StartsConsumption()
    {
        // Arrange
        var handler = new Mock<Func<IMessage, Task>>();
        await _consumer.SubscribeAsync(TestTopic, handler.Object);

        // Act
        await _consumer.StartAsync();

        // Assert
        Assert.True(_consumer.IsRunning);
        _mockConsumer.Verify(c => c.Subscribe(TestTopic), Times.Once);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNothing()
    {
        // Arrange
        var handler = new Mock<Func<IMessage, Task>>();
        await _consumer.SubscribeAsync(TestTopic, handler.Object);
        await _consumer.StartAsync();

        // Act
        await _consumer.StartAsync();

        // Assert
        _mockConsumer.Verify(c => c.Subscribe(TestTopic), Times.Once);
    }

    [Fact]
    public async Task StopAsync_WhenRunning_StopsConsumption()
    {
        // Arrange
        var handler = new Mock<Func<IMessage, Task>>();
        await _consumer.SubscribeAsync(TestTopic, handler.Object);
        await _consumer.StartAsync();

        // Act
        await _consumer.StopAsync();

        // Assert
        Assert.False(_consumer.IsRunning);
        _mockConsumer.Verify(c => c.Unsubscribe(), Times.Once);
        _mockConsumer.Verify(c => c.Close(), Times.Once);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNothing()
    {
        // Act
        await _consumer.StopAsync();

        // Assert
        _mockConsumer.Verify(c => c.Unsubscribe(), Times.Never);
        _mockConsumer.Verify(c => c.Close(), Times.Never);
    }

    [Fact]
    public async Task SubscribeAsync_AddsHandlerAndSubscribes()
    {
        // Arrange
        var handler = new Mock<Func<IMessage, Task>>();
        await _consumer.StartAsync();

        // Act
        await _consumer.SubscribeAsync(TestTopic, handler.Object);

        // Assert
        _mockConsumer.Verify(c => c.Subscribe(TestTopic), Times.Once);
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesHandlerAndUnsubscribes()
    {
        // Arrange
        var handler = new Mock<Func<IMessage, Task>>();
        await _consumer.SubscribeAsync(TestTopic, handler.Object);
        await _consumer.StartAsync();

        // Act
        await _consumer.UnsubscribeAsync(TestTopic);

        // Assert
        _mockConsumer.Verify(c => c.Unsubscribe(), Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_WhenDeserializationFails_LogsError()
    {
        // Arrange
        var handler = new Mock<Func<IMessage, Task>>();
        await _consumer.SubscribeAsync(TestTopic, handler.Object);
        await _consumer.StartAsync();

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = TestTopic,
            Message = new Message<string, string>
            {
                Key = "test-type",
                Value = "invalid-json"
            }
        };

        _mockConsumer.Setup(c => c.Consume(It.IsAny<CancellationToken>()))
            .Returns(consumeResult);

        // Act & Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error consuming message")),
                It.IsAny<JsonException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_WhenHandlerThrows_LogsError()
    {
        // Arrange
        var exception = new Exception("Test error");
        var handler = new Mock<Func<IMessage, Task>>();
        handler.Setup(h => h(It.IsAny<IMessage>())).ThrowsAsync(exception);

        await _consumer.SubscribeAsync(TestTopic, handler.Object);
        await _consumer.StartAsync();

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = TestTopic,
            Message = new Message<string, string>
            {
                Key = "test-type",
                Value = JsonSerializer.Serialize(_testMessage)
            }
        };

        _mockConsumer.Setup(c => c.Consume(It.IsAny<CancellationToken>()))
            .Returns(consumeResult);

        // Act & Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error consuming message")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
} 