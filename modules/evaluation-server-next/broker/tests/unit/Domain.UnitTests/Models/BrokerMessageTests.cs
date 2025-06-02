using FeatBit.EvaluationServer.Broker.Domain.Models;
using Xunit;

namespace Domain.UnitTests.Models;

public class BrokerMessageTests
{
    [Fact]
    public void Constructor_CreatesInstanceWithDefaultValues()
    {
        // Act
        var message = new BrokerMessage();

        // Assert
        Assert.NotNull(message);
        Assert.Empty(message.Topic);
        Assert.Empty(message.MessageType);
        Assert.Empty(message.Payload);
        Assert.NotNull(message.Metadata);
        Assert.Empty(message.Metadata.Source);
        Assert.Empty(message.Metadata.CorrelationId);
        Assert.NotNull(message.Metadata.Headers);
        Assert.Empty(message.Metadata.Headers);
    }

    [Fact]
    public void Constructor_TimestampDefaultsToUtcNow()
    {
        // Arrange
        var beforeCreate = DateTimeOffset.UtcNow;
        
        // Act
        var message = new BrokerMessage();
        var afterCreate = DateTimeOffset.UtcNow;

        // Assert
        Assert.True(message.Timestamp >= beforeCreate);
        Assert.True(message.Timestamp <= afterCreate);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var topic = "test-topic";
        var messageType = "test-type";
        var payload = "test-payload";
        var timestamp = DateTimeOffset.UtcNow;
        var source = "test-source";
        var correlationId = "test-correlation-id";
        var headers = new Dictionary<string, string> { { "key", "value" } };

        // Act
        var message = new BrokerMessage
        {
            Topic = topic,
            MessageType = messageType,
            Payload = payload,
            Timestamp = timestamp,
            Metadata = new MessageMetadata
            {
                Source = source,
                CorrelationId = correlationId,
                Headers = headers
            }
        };

        // Assert
        Assert.Equal(topic, message.Topic);
        Assert.Equal(messageType, message.MessageType);
        Assert.Equal(payload, message.Payload);
        Assert.Equal(timestamp, message.Timestamp);
        Assert.Equal(source, message.Metadata.Source);
        Assert.Equal(correlationId, message.Metadata.CorrelationId);
        Assert.Equal(headers, message.Metadata.Headers);
    }

    [Fact]
    public void Metadata_CanBeModifiedAfterCreation()
    {
        // Arrange
        var message = new BrokerMessage();
        var headers = new Dictionary<string, string> { { "key", "value" } };

        // Act
        message.Metadata.Source = "new-source";
        message.Metadata.CorrelationId = "new-correlation-id";
        message.Metadata.Headers = headers;

        // Assert
        Assert.Equal("new-source", message.Metadata.Source);
        Assert.Equal("new-correlation-id", message.Metadata.CorrelationId);
        Assert.Equal(headers, message.Metadata.Headers);
    }
} 