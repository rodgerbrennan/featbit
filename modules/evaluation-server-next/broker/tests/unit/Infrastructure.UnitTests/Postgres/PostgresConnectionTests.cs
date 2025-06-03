using FeatBit.EvaluationServer.Broker.Domain.Models;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using FeatBit.EvaluationServer.Broker.Infrastructure.Postgres;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Xunit;

namespace Infrastructure.UnitTests.Postgres;

public class PostgresConnectionTests
{
    private readonly Mock<IOptions<PostgresOptions>> _mockOptions;
    private readonly PostgresOptions _options;
    private readonly Mock<ILogger<PostgresConnection>> _mockLogger;
    private readonly Mock<IPostgresDataSource> _mockDataSource;
    private readonly TestablePostgresConnection _connection;

    public PostgresConnectionTests()
    {
        _options = new PostgresOptions
        {
            ConnectionString = "Host=localhost;Database=testdb;Username=test;Password=test",
            SchemaName = "public",
            MessagesTableName = "messages",
            SubscriptionsTableName = "subscriptions"
        };

        _mockOptions = new Mock<IOptions<PostgresOptions>>();
        _mockOptions.Setup(o => o.Value).Returns(_options);

        _mockLogger = new Mock<ILogger<PostgresConnection>>();
        _mockDataSource = new Mock<IPostgresDataSource>();

        _connection = new TestablePostgresConnection(
            _mockOptions.Object,
            _mockLogger.Object,
            _mockDataSource.Object);
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        Assert.NotNull(_connection);
        Assert.False(_connection.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_InitializesDataSource()
    {
        // Act
        await _connection.ConnectAsync();

        // Assert
        Assert.True(_connection.IsConnected);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetDataSource_NotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _connection.GetDataSource());
        Assert.Equal("PostgreSQL connection is not initialized. Call ConnectAsync first.", exception.Message);
    }

    [Fact]
    public async Task GetDataSource_WhenConnected_ReturnsDataSource()
    {
        // Arrange
        await _connection.ConnectAsync();

        // Act
        var dataSource = _connection.GetDataSource();

        // Assert
        Assert.Same(_mockDataSource.Object, dataSource);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenConnected_ReturnsTrue()
    {
        // Arrange
        await _connection.ConnectAsync();

        var mockConnection = new Mock<NpgsqlConnection>();
        var mockCommand = new Mock<NpgsqlCommand>();
        mockCommand.Setup(c => c.ExecuteScalarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockDataSource.Setup(ds => ds.OpenConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockConnection.Object);

        // Act
        var result = await _connection.TestConnectionAsync();

        // Assert
        Assert.True(result);
        _mockDataSource.Verify(ds => ds.OpenConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task TestConnectionAsync_WhenQueryFails_ReturnsFalse()
    {
        // Arrange
        await _connection.ConnectAsync();

        var mockConnection = new Mock<NpgsqlConnection>();
        var mockCommand = new Mock<NpgsqlCommand>();
        mockCommand.Setup(c => c.ExecuteScalarAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NpgsqlException("Test error"));

        _mockDataSource.Setup(ds => ds.OpenConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockConnection.Object);

        // Act
        var result = await _connection.TestConnectionAsync();

        // Assert
        Assert.False(result);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_DisposesDataSource()
    {
        // Arrange
        await _connection.ConnectAsync();

        // Act
        await _connection.DisposeAsync();

        // Assert
        Assert.False(_connection.IsConnected);
        _mockDataSource.Verify(ds => ds.DisposeAsync(), Times.Once);
    }
}

public class TestablePostgresConnection : PostgresConnection
{
    private readonly IPostgresDataSource _dataSource;

    public TestablePostgresConnection(
        IOptions<PostgresOptions> options,
        ILogger<PostgresConnection> logger,
        IPostgresDataSource dataSource)
        : base(options, logger)
    {
        _dataSource = dataSource;
    }

    protected override IPostgresDataSource CreateDataSource(string connectionString)
    {
        return _dataSource;
    }
}