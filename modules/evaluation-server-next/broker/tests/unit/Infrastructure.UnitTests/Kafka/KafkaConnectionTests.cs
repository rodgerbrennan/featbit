using Confluent.Kafka;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using FeatBit.EvaluationServer.Broker.Infrastructure.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Infrastructure.UnitTests.Kafka;

public class KafkaConnectionTests
{
    private readonly Mock<IOptions<KafkaOptions>> _mockOptions;
    private readonly Mock<ILogger<KafkaConnection>> _mockLogger;
    private readonly KafkaOptions _options;
    private readonly KafkaConnection _connection;
    private readonly Mock<IAdminClient> _mockAdminClient;
    private readonly Mock<IProducer<string, string>> _mockProducer;
    private readonly Mock<IConsumer<string, string>> _mockConsumer;

    public KafkaConnectionTests()
    {
        _options = new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            GroupId = "test-group",
            ClientId = "test-client"
        };

        _mockOptions = new Mock<IOptions<KafkaOptions>>();
        _mockOptions.Setup(o => o.Value).Returns(_options);
        
        _mockLogger = new Mock<ILogger<KafkaConnection>>();
        
        // Mock Kafka dependencies
        _mockAdminClient = new Mock<IAdminClient>();
        _mockProducer = new Mock<IProducer<string, string>>();
        _mockConsumer = new Mock<IConsumer<string, string>>();

        // Create a testable version of KafkaConnection
        _connection = new TestableKafkaConnection(
            _mockOptions.Object,
            _mockLogger.Object,
            _mockAdminClient.Object,
            _mockProducer.Object,
            _mockConsumer.Object);
    }

    [Fact]
    public void IsConnected_InitialState_ReturnsFalse()
    {
        // Assert
        Assert.False(_connection.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_ValidConfiguration_ConnectsSuccessfully()
    {
        // Arrange
        var metadata = new Metadata(new List<BrokerMetadata> { new BrokerMetadata(1, "localhost", 9092) }, 
            new List<TopicMetadata>(), 0, "");
            
        _mockAdminClient.Setup(a => a.GetMetadata(TimeSpan.FromSeconds(5)))
            .Returns(metadata);

        // Act
        await _connection.ConnectAsync();

        // Assert
        Assert.True(_connection.IsConnected);
    }

    [Fact]
    public async Task GetProducer_NotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _connection.GetProducer());
        Assert.Equal("Kafka producer is not initialized. Call ConnectAsync first.", exception.Message);
    }

    [Fact]
    public async Task GetConsumer_NotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _connection.GetConsumer());
        Assert.Equal("Kafka consumer is not initialized. Call ConnectAsync first.", exception.Message);
    }

    [Fact]
    public async Task DisposeAsync_DisposesProducerAndConsumer()
    {
        // Arrange
        var metadata = new Metadata(new List<BrokerMetadata> { new BrokerMetadata(1, "localhost", 9092) }, 
            new List<TopicMetadata>(), 0, "");
            
        _mockAdminClient.Setup(a => a.GetMetadata(TimeSpan.FromSeconds(5)))
            .Returns(metadata);
            
        await _connection.ConnectAsync();

        // Act
        await _connection.DisposeAsync();

        // Assert
        Assert.False(_connection.IsConnected);
        _mockProducer.Verify(p => p.Dispose(), Times.Once);
        _mockConsumer.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task TestConnectionAsync_ValidConfiguration_ReturnsTrue()
    {
        // Arrange
        var metadata = new Metadata(new List<BrokerMetadata> { new BrokerMetadata(1, "localhost", 9092) }, 
            new List<TopicMetadata>(), 0, "");
            
        _mockAdminClient.Setup(a => a.GetMetadata(TimeSpan.FromSeconds(5)))
            .Returns(metadata);

        // Act
        var result = await _connection.TestConnectionAsync();

        // Assert
        Assert.True(result);
    }
}

// Testable version of KafkaConnection that allows injection of mocked dependencies
public class TestableKafkaConnection : KafkaConnection
{
    private readonly IAdminClient _adminClient;
    private readonly IProducer<string, string> _producer;
    private readonly IConsumer<string, string> _consumer;

    public TestableKafkaConnection(
        IOptions<KafkaOptions> options,
        ILogger<KafkaConnection> logger,
        IAdminClient adminClient,
        IProducer<string, string> producer,
        IConsumer<string, string> consumer)
        : base(options, logger)
    {
        _adminClient = adminClient;
        _producer = producer;
        _consumer = consumer;
    }

    protected override IAdminClient CreateAdminClient()
    {
        return _adminClient;
    }

    protected override IProducer<string, string> CreateProducer()
    {
        return _producer;
    }

    protected override IConsumer<string, string> CreateConsumer()
    {
        return _consumer;
    }
} 