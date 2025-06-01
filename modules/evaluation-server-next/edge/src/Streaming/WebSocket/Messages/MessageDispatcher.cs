using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FeatBit.EvaluationServer.Shared.Messages;
using FeatBit.EvaluationServer.Shared.Metrics;
using FeatBit.EvaluationServer.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Edge.WebSocket.Messages;

public sealed partial class MessageDispatcher
{
    // JSON message format: {"messageType": "<type>", "data": <object>}
    private const string MessageTypePropertyName = "messageType";
    private const string DataPropertyName = "data";

    // Default buffer size for receiving messages (in bytes)
    private const int DefaultBufferSize = 2 * 1024;

    // Maximum number of fragments for a message, for most of the time messages should be single-fragment
    private const int MaxMessageFragment = 4;

    private readonly Dictionary<string, IMessageHandler> _handlers;
    private readonly ILogger<MessageDispatcher> _logger;
    private readonly IStreamingMetrics _metrics;

    public MessageDispatcher(
        IEnumerable<IMessageHandler> handlers,
        ILogger<MessageDispatcher> logger,
        IStreamingMetrics metrics)
    {
        _handlers = handlers.ToDictionary(handler => handler.Type, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
        _metrics = metrics;
    }

    public async Task DispatchAsync(ConnectionContext connection, CancellationToken token)
    {
        var ws = connection.WebSocket;

        while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            try
            {
                await DispatchCoreAsync(connection, token);
            }
            catch (WebSocketException e) when (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                ws.Abort();
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching message for connection");
                _metrics.ConnectionError("MessageDispatchError");
            }
        }
    }

    private async Task DispatchCoreAsync(ConnectionContext connection, CancellationToken token)
    {
        var ws = connection.WebSocket;
        var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        var fragments = new List<byte>();
        var fragmentCount = 0;

        try
        {
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogTrace("Received close message");
                    return;
                }

                if (result.Count == 0)
                {
                    _logger.LogTrace("Received empty message");
                    continue;
                }

                fragmentCount++;
                if (fragmentCount > MaxMessageFragment)
                {
                    _logger.LogWarning("Too many fragments for message");
                    _metrics.ConnectionError("TooManyFragments");
                    return;
                }

                fragments.AddRange(buffer.AsSpan(0, result.Count).ToArray());
            } while (!result.EndOfMessage);

            if (fragments.Count == 0)
            {
                return;
            }

            await HandleMessageAsync(connection, fragments.ToArray(), token);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            fragments.Clear();
        }
    }

    private async Task HandleMessageAsync(ConnectionContext connection, byte[] bytes, CancellationToken token)
    {
        try
        {
            var message = JsonSerializer.Deserialize<JsonDocument>(bytes);
            if (message == null)
            {
                _logger.LogWarning("Failed to deserialize message");
                _metrics.ConnectionError("InvalidMessageFormat");
                return;
            }

            var messageType = message.RootElement.GetProperty(MessageTypePropertyName).GetString();
            if (string.IsNullOrEmpty(messageType))
            {
                _logger.LogWarning("Invalid message type");
                _metrics.ConnectionError("InvalidMessageType");
                return;
            }

            if (!_handlers.TryGetValue(messageType, out var handler))
            {
                _logger.LogWarning("Unknown message type: {MessageType}", messageType);
                _metrics.ConnectionError("UnknownMessageType");
                return;
            }

            JsonElement data;
            try
            {
                data = message.RootElement.GetProperty(DataPropertyName);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Missing required 'data' property");
                _metrics.ConnectionError("InvalidMessageFormat");
                return;
            }

            var ctx = new MessageContext(connection, data, token);

            using var processingTimer = _metrics.TrackMessageProcessing(messageType, bytes.Length);
            await handler.HandleAsync(ctx);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid message format");
            _metrics.ConnectionError("InvalidMessageFormat");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message");
            _metrics.ConnectionError("MessageHandlingError");
        }
    }
} 