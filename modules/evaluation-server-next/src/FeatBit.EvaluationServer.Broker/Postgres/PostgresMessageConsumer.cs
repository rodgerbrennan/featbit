using System.Threading.Channels;
using Dapper;
using FeatBit.EvaluationServer.Shared.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FeatBit.EvaluationServer.Broker.Postgres;

internal record ChannelMessage(string Channel, long MessageId);

public sealed class PostgresMessageConsumer : BackgroundService, IMessageConsumer
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly Dictionary<string, IMessageHandler> _handlers;
    private readonly ILogger<PostgresMessageConsumer> _logger;

    private static readonly Channel<ChannelMessage> MessageChannel = Channel.CreateBounded<ChannelMessage>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true
        }
    );

    // The interval in seconds to wait before restarting the listen task after connection closed.
    private const int RestartIntervalInSeconds = 5;

    // The number of seconds of connection inactivity before Npgsql sends a keepalive query.
    private const int KeepAliveIntervalInSeconds = 15;

    // The cancellation token source for the listen task
    private CancellationTokenSource _listenCts = new();

    // The connection being used to listen for notifications, we record it so we can dispose it when reconnecting
    private NpgsqlConnection? _connection;

    // The time when the connection was closed
    private DateTime? _connectionClosedAt;

    // The message id of the last message consumed
    private long _lastMessageId = -1;

    // The time when we start the listen task
    private DateTime? _startedAt;

    private const string FetchMissedMessagesSql = """
        select id, topic
        from queue_messages
        where not_visible_until is null
          and topic = any (@Topics)
          and status = 'Notified'
          and (
            (@LastMessageId = -1 and enqueued_at > @LastEnqueuedAt) or
            (@LastMessageId != -1 and id > @LastMessageId)
            )
        order by id;
        """;

    public PostgresMessageConsumer(
        NpgsqlDataSource dataSource,
        IEnumerable<IMessageHandler> handlers,
        ILogger<PostgresMessageConsumer> logger)
    {
        _dataSource = dataSource;
        _handlers = handlers.ToDictionary(x => Topics.ToChannel(x.Topic), x => x);
        _logger = logger;
    }

    public async Task SubscribeAsync(string channel, Func<string, Task> handler)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync($"LISTEN {channel};");
    }

    public async Task UnsubscribeAsync(string channel)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync($"UNLISTEN {channel};");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = new[]
        {
            ListenAsync(stoppingToken),
            ConsumeAsync(stoppingToken)
        };

        return Task.WhenAll(tasks);
    }

    private async Task ListenAsync(CancellationToken stoppingToken)
    {
        _startedAt = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            var startError = false;

            try
            {
                var connection = await SetupConnectionAsync();

                // add missed messages if any
                await AddMissedMessagesAsync(connection);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_listenCts.Token, stoppingToken);

                // Listen to all channels
                var listenChannelsSql = string.Join(' ', _handlers.Keys.Select(x => $"LISTEN {x};"));
                await connection.ExecuteAsync(listenChannelsSql, cts.Token);

                _logger.LogInformation("Start listening on channels: {Channels}", string.Join(',', _handlers.Keys));

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        _logger.LogDebug("Waiting for notification...");
                        await connection.WaitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error waiting for notification");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                startError = true;
                _logger.LogError(ex, "Error starting listener. Will restart in {Seconds} seconds", RestartIntervalInSeconds);
            }

            // the listen task is stopped due to start error
            if (startError)
            {
                _logger.LogWarning("Listen stopped due to start error. Restarting in {Seconds} seconds", RestartIntervalInSeconds);
                await Task.Delay(TimeSpan.FromSeconds(RestartIntervalInSeconds), stoppingToken);
            }
            // the listen task is stopped due to connection closed
            else if (_listenCts.IsCancellationRequested)
            {
                _logger.LogWarning("Listen stopped due to connection closed. Restarting in {Seconds} seconds", RestartIntervalInSeconds);
                await Task.Delay(TimeSpan.FromSeconds(RestartIntervalInSeconds), stoppingToken);

                // reset the cancellation token source
                _listenCts.Dispose();
                _listenCts = new CancellationTokenSource();
            }
        }

        _logger.LogInformation("Listening stopped");
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in MessageChannel.Reader.ReadAllAsync(stoppingToken))
        {
            var (channel, messageId) = message;

            try
            {
                await ConsumeCoreAsync(channel, messageId);
                _logger.LogDebug("Message handled: {Id}", messageId);

                _lastMessageId = messageId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming message: {Channel}", channel);
            }
        }

        return;

        async Task ConsumeCoreAsync(string channel, long messageId)
        {
            if (!_handlers.TryGetValue(channel, out var handler))
            {
                _logger.LogWarning("No handler for channel: {Channel}", channel);
                return;
            }

            await using var connection = await _dataSource.OpenConnectionAsync(stoppingToken);
            var payload = await connection.QueryFirstOrDefaultAsync<string>(
                "select payload from queue_messages where id = @Id", new { Id = messageId }
            );

            if (string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            await handler.HandleAsync(payload, stoppingToken);
        }
    }

    private async Task<NpgsqlConnection> SetupConnectionAsync()
    {
        if (_connection != null)
        {
            try
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
                _connectionClosedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing connection");
            }
        }

        var connection = await _dataSource.OpenConnectionAsync();
        connection.Notification += OnNotification;
        connection.StateChange += (_, args) =>
        {
            if (args.CurrentState == System.Data.ConnectionState.Closed)
            {
                _connectionClosedAt = DateTime.UtcNow;
            }
        };

        _connection = connection;
        return connection;
    }

    private void OnNotification(object sender, NpgsqlNotificationEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Payload) || !long.TryParse(args.Payload, out var messageId))
        {
            _logger.LogWarning("Invalid message received: {Payload}", args.Payload);
            return;
        }

        _logger.LogDebug("Notification received - Channel: {Channel}, PID: {PID}, MessageId: {MessageId}",
            args.Channel, args.PID, args.Payload);

        MessageChannel.Writer.TryWrite(new ChannelMessage(args.Channel, messageId));
    }

    private async Task AddMissedMessagesAsync(NpgsqlConnection connection)
    {
        try
        {
            // if the connection is closed, use the connection closed time;
            // otherwise, use the time when the listen task started
            var lastEnqueuedAt = _connectionClosedAt ?? _startedAt;

            var missingMessages = await connection.QueryAsync<(long id, string topic)>(
                FetchMissedMessagesSql,
                new
                {
                    Topics = _handlers.Values.Select(x => x.Topic).ToArray(),
                    LastMessageId = _lastMessageId,
                    LastEnqueuedAt = lastEnqueuedAt
                }
            );

            foreach (var message in missingMessages)
            {
                var channelMessage = new ChannelMessage(Topics.ToChannel(message.topic), message.id);
                MessageChannel.Writer.TryWrite(channelMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding missed messages");
        }
    }

    public override void Dispose()
    {
        if (_connection != null)
        {
            _connection.Notification -= OnNotification;
            _connection.Dispose();
        }

        _listenCts.Dispose();
        base.Dispose();
    }
} 