using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using FeatBit.EvaluationServer.Broker.Infrastructure.Redis;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Infrastructure.UnitTests.Redis;

public class RedisConnectionTests
{
    private readonly Mock<IOptions<RedisOptions>> _mockOptions;
    private readonly RedisOptions _options;
    private readonly Mock<IConnectionMultiplexer> _mockMultiplexer;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ISubscriber> _mockSubscriber;
    private readonly TestableRedisConnection _connection;

    public RedisConnectionTests()
    {
        _options = new RedisOptions
        {
            ConnectionString = "localhost:6379",
            Password = "test",
            Database = 0
        };

        _mockOptions = new Mock<IOptions<RedisOptions>>();
        _mockOptions.Setup(o => o.Value).Returns(_options);

        _mockMultiplexer = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockSubscriber = new Mock<ISubscriber>();

        _mockMultiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);
        _mockMultiplexer.Setup(m => m.GetSubscriber(It.IsAny<object>()))
            .Returns(_mockSubscriber.Object);

        _connection = new TestableRedisConnection(
            _mockOptions.Object,
            _mockMultiplexer.Object);
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        Assert.NotNull(_connection);
        Assert.False(_connection.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_SetsIsConnectedToTrue()
    {
        // Act
        await _connection.ConnectAsync();

        // Assert
        Assert.True(_connection.IsConnected);
    }

    [Fact]
    public async Task GetDatabase_WhenConnected_ReturnsDatabaseWithCorrectNumber()
    {
        // Arrange
        await _connection.ConnectAsync();

        // Act
        var database = _connection.GetDatabase();

        // Assert
        Assert.Same(_mockDatabase.Object, database);
        _mockMultiplexer.Verify(m => m.GetDatabase(_options.Database, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public void GetDatabase_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _connection.GetDatabase());
        Assert.Equal("Redis connection is not initialized. Call ConnectAsync first.", exception.Message);
    }

    [Fact]
    public async Task GetSubscriber_WhenConnected_ReturnsSubscriber()
    {
        // Arrange
        await _connection.ConnectAsync();

        // Act
        var subscriber = _connection.GetSubscriber();

        // Assert
        Assert.Same(_mockSubscriber.Object, subscriber);
        _mockMultiplexer.Verify(m => m.GetSubscriber(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public void GetSubscriber_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _connection.GetSubscriber());
        Assert.Equal("Redis connection is not initialized. Call ConnectAsync first.", exception.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenNotConnected_ReturnsFalse()
    {
        // Act
        var result = await _connection.TestConnectionAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DisposeAsync_SetsIsConnectedToFalse()
    {
        // Arrange
        await _connection.ConnectAsync();

        // Act
        await _connection.DisposeAsync();

        // Assert
        Assert.False(_connection.IsConnected);
    }
}

// Testable version of RedisConnection that allows injection of mocked dependencies
public class TestableRedisConnection : RedisConnection
{
    private readonly IConnectionMultiplexer _multiplexer;

    public TestableRedisConnection(
        IOptions<RedisOptions> options,
        IConnectionMultiplexer multiplexer)
        : base(options)
    {
        _multiplexer = multiplexer;
    }

    protected override Task<IConnectionMultiplexer> CreateConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_multiplexer);
    }
} 