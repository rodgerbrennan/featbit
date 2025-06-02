using FeatBit.EvaluationServer.Broker.Core.Services;
using FeatBit.EvaluationServer.Broker.Domain.Messages;
using FeatBit.EvaluationServer.Broker.Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Core.UnitTests.Services;

public class MessageRoutingServiceTests
{
    private readonly Mock<IMessageProducer> _mockProducer;
    private readonly Mock<IMessageConsumer> _mockConsumer;
    private readonly Mock<ILogger<MessageRoutingService>> _mockLogger;
    private readonly MessageRoutingService _service;

    public MessageRoutingServiceTests()
    {
        _mockProducer = new Mock<IMessageProducer>();
        _mockConsumer = new Mock<IMessageConsumer>();
        _mockLogger = new Mock<ILogger<MessageRoutingService>>();
        _service = new MessageRoutingService(_mockProducer.Object, _mockConsumer.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task RouteMessageAsync_ValidMessage_CallsProducerPublishAsync()
    {
        // Arrange
        var message = new BrokerMessage
        {
            Topic = "test-topic",
            MessageType = "test-type",
            Payload = "test-payload"
        };

        _mockProducer.Setup(p => p.PublishAsync(message, default))
            .Returns(Task.CompletedTask);

        // Act
        await _service.RouteMessageAsync(message);

        // Assert
        _mockProducer.Verify(p => p.PublishAsync(message, default), Times.Once);
    }

    [Fact]
    public async Task RouteMessageAsync_ProducerThrowsException_LogsErrorAndRethrows()
    {
        // Arrange
        var message = new BrokerMessage
        {
            Topic = "test-topic",
            MessageType = "test-type",
            Payload = "test-payload"
        };

        var exception = new Exception("Test exception");
        _mockProducer.Setup(p => p.PublishAsync(message, default))
            .ThrowsAsync(exception);

        // Act & Assert
        var thrownException = await Assert.ThrowsAsync<Exception>(() => 
            _service.RouteMessageAsync(message));
        
        Assert.Same(exception, thrownException);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateRouteAsync_CallsProducerValidateTopicAsync()
    {
        // Arrange
        var topic = "test-topic";
        _mockProducer.Setup(p => p.ValidateTopicAsync(topic))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ValidateRouteAsync(topic);

        // Assert
        Assert.True(result);
        _mockProducer.Verify(p => p.ValidateTopicAsync(topic), Times.Once);
    }

    [Fact]
    public async Task SubscribeAsync_CallsConsumerSubscribeAsync()
    {
        // Arrange
        var topic = "test-topic";
        Func<IMessage, Task> handler = _ => Task.CompletedTask;

        _mockConsumer.Setup(c => c.SubscribeAsync(topic, handler))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SubscribeAsync(topic, handler);

        // Assert
        _mockConsumer.Verify(c => c.SubscribeAsync(topic, handler), Times.Once);
    }

    [Fact]
    public async Task UnsubscribeAsync_CallsConsumerUnsubscribeAsync()
    {
        // Arrange
        var topic = "test-topic";
        _mockConsumer.Setup(c => c.UnsubscribeAsync(topic))
            .Returns(Task.CompletedTask);

        // Act
        await _service.UnsubscribeAsync(topic);

        // Assert
        _mockConsumer.Verify(c => c.UnsubscribeAsync(topic), Times.Once);
    }
} 