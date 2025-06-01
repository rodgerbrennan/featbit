using FeatBit.EvaluationServer.Broker.Domain.Brokers;
using FeatBit.EvaluationServer.Broker.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FeatBit.EvaluationServer.Broker.Infrastructure.Postgres;

public class PostgresConnection : IBrokerConnection
{
    private readonly IOptions<PostgresOptions> _options;
    private readonly ILogger<PostgresConnection> _logger;
    private NpgsqlDataSource? _dataSource;
    private bool _isConnected;

    public PostgresConnection(
        IOptions<PostgresOptions> options,
        ILogger<PostgresConnection> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsConnected => _isConnected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(_options.Value.ConnectionString);
            _dataSource = dataSourceBuilder.Build();

            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await InitializeSchemaAsync(connection, cancellationToken);

            _isConnected = true;
            _logger.LogInformation("Successfully connected to PostgreSQL");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to PostgreSQL");
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (_dataSource == null)
        {
            return false;
        }

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test PostgreSQL connection");
            return false;
        }
    }

    public NpgsqlDataSource GetDataSource()
    {
        if (_dataSource == null)
        {
            throw new InvalidOperationException("PostgreSQL connection is not initialized. Call ConnectAsync first.");
        }
        return _dataSource;
    }

    private async Task InitializeSchemaAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        
        // Create messages table
        command.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {_options.Value.SchemaName}.{_options.Value.MessagesTableName} (
                id BIGSERIAL PRIMARY KEY,
                topic VARCHAR(255) NOT NULL,
                message_type VARCHAR(255) NOT NULL,
                payload TEXT NOT NULL,
                timestamp TIMESTAMP WITH TIME ZONE NOT NULL,
                metadata JSONB,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_{_options.Value.MessagesTableName}_topic 
                ON {_options.Value.SchemaName}.{_options.Value.MessagesTableName}(topic);
            CREATE INDEX IF NOT EXISTS idx_{_options.Value.MessagesTableName}_timestamp 
                ON {_options.Value.SchemaName}.{_options.Value.MessagesTableName}(timestamp);
        ";
        await command.ExecuteNonQueryAsync(cancellationToken);

        // Create subscriptions table
        command.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {_options.Value.SchemaName}.{_options.Value.SubscriptionsTableName} (
                id BIGSERIAL PRIMARY KEY,
                topic VARCHAR(255) NOT NULL,
                last_processed_id BIGINT NOT NULL DEFAULT 0,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(topic)
            );
        ";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource != null)
        {
            await _dataSource.DisposeAsync();
            _isConnected = false;
        }
    }
} 