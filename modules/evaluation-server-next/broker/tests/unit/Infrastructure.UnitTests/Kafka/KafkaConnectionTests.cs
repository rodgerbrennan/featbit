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
    private readonly KafkaOptions _options;
    private readonly Mock<ILogger<KafkaConnection>> _mockLogger;
    private readonly Mock<IProducer<string, string>> _mockProducer;
    private readonly Mock<IConsumer<string, string>> _mockConsumer;
    private readonly Mock<IAdminClient> _mockAdminClient;
    private readonly TestableKafkaConnection _connection;

    public KafkaConnectionTests()
    {
        _options = new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            GroupId = "test-group",
            ClientId = "test-client",
            AutoOffsetReset = "Latest",
            EnableAutoCommit = true,
            AllowAutoCreateTopics = true
        };

        _mockOptions = new Mock<IOptions<KafkaOptions>>();
        _mockOptions.Setup(o => o.Value).Returns(_options);

        _mockLogger = new Mock<ILogger<KafkaConnection>>();
        _mockProducer = new Mock<IProducer<string, string>>();
        _mockConsumer = new Mock<IConsumer<string, string>>();
        _mockAdminClient = new Mock<IAdminClient>();

        _mockAdminClient.Setup(a => a.GetMetadata(It.IsAny<TimeSpan>()))
            .Returns(new Metadata(
                new List<BrokerMetadata> { new BrokerMetadata(1, "localhost", 9092) },
                new List<TopicMetadata>(),
                1,
                "test-broker"));

        _connection = new TestableKafkaConnection(
            _mockOptions.Object,
            _mockLogger.Object,
            _mockProducer.Object,
            _mockConsumer.Object,
            _mockAdminClient.Object);
    }

    [Fact]
    public void Constructor_InitializesConnection()
    {
        // Assert
        Assert.NotNull(_connection);
        Assert.False(_connection.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_InitializesProducerAndConsumer()
    {
        // Act
        await _connection.ConnectAsync();

        // Assert
        Assert.True(_connection.IsConnected);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Connected")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetProducer_NotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _connection.GetProducer());
        Assert.Equal("Kafka producer is not initialized. Call ConnectAsync first.", exception.Message);
    }

    [Fact]
    public async Task GetProducer_WhenConnected_ReturnsProducer()
    {
        // Arrange
        await _connection.ConnectAsync();

        // Act
        var producer = _connection.GetProducer();

        // Assert
        Assert.Same(_mockProducer.Object, producer);
    }

    [Fact]
    public void GetConsumer_NotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _connection.GetConsumer());
        Assert.Equal("Kafka consumer is not initialized. Call ConnectAsync first.", exception.Message);
    }

    [Fact]
    public async Task GetConsumer_WhenConnected_ReturnsConsumer()
    {
        // Arrange
        await _connection.ConnectAsync();

        // Act
        var consumer = _connection.GetConsumer();

        // Assert
        Assert.Same(_mockConsumer.Object, consumer);
    }

    [Fact]
    public async Task DisposeAsync_DisposesProducerAndConsumer()
    {
        // Arrange
        await _connection.ConnectAsync();

        // Act
        await _connection.DisposeAsync();

        // Assert
        Assert.False(_connection.IsConnected);
        _mockProducer.Verify(p => p.Dispose(), Times.Once);
        _mockConsumer.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenDisposalThrows_LogsError()
    {
        // Arrange
        await _connection.ConnectAsync();
        var exception = new KafkaException(new Error(ErrorCode.Local_Fatal, "Test error"));
        _mockProducer.Setup(p => p.Dispose())
            .Throws(exception);

        // Act
        await _connection.DisposeAsync();

        // Assert
        Assert.False(_connection.IsConnected);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to dispose")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

public class TestableKafkaConnection : KafkaConnection
{
    private readonly IProducer<string, string> _producer;
    private readonly IConsumer<string, string> _consumer;
    private readonly IAdminClient _adminClient;

    public TestableKafkaConnection(
        IOptions<KafkaOptions> options,
        ILogger<KafkaConnection> logger,
        IProducer<string, string> producer,
        IConsumer<string, string> consumer,
        IAdminClient adminClient)
        : base(options, logger)
    {
        _producer = producer;
        _consumer = consumer;
        _adminClient = adminClient;
    }

    protected override IProducer<string, string> CreateProducer()
    {
        return _producer;
    }

    protected override IConsumer<string, string> CreateConsumer()
    {
        return _consumer;
    }

    protected override IAdminClient CreateAdminClient()
    {
        return _adminClient;
    }
} 