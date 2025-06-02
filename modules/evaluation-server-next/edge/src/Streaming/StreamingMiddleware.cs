using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.EvaluationServer.Edge.Domain.Interfaces;
using FeatBit.EvaluationServer.Edge.Domain.Common.Models;
using FeatBit.EvaluationServer.Edge.WebSocket.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Edge.Streaming;

public class StreamingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly FeatBit.EvaluationServer.Edge.Domain.Interfaces.IConnectionManager _connectionManager;
    private readonly ILogger<StreamingMiddleware> _logger;

    public StreamingMiddleware(
        RequestDelegate next,
        FeatBit.EvaluationServer.Edge.Domain.Interfaces.IConnectionManager connectionManager,
        ILogger<StreamingMiddleware> logger)
    {
        _next = next;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        try
        {
            // Create connection context
            var secrets = new[] { new Secret { EnvId = Guid.NewGuid() } }; // This should come from validation
            var connectionContext = await DefaultConnectionContext.CreateAsync(webSocket, context, secrets);
            await _connectionManager.Add(connectionContext);

            // Keep the connection alive until it's closed
            try
            {
                await HandleWebSocketConnection(connectionContext);
            }
            finally
            {
                await _connectionManager.Remove(connectionContext.Connection.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket connection");
            await CloseWebSocketConnection(webSocket);
        }
    }

    private async Task HandleWebSocketConnection(ConnectionContext context)
    {
        var buffer = new byte[1024 * 4];
        var webSocket = context.WebSocket;

        while (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
            {
                await CloseWebSocketConnection(webSocket);
                break;
            }

            // Handle received message
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Binary)
            {
                // Message handling will be implemented in MessageDispatcher
            }
        }
    }

    private async Task CloseWebSocketConnection(System.Net.WebSockets.WebSocket webSocket)
    {
        if (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
        {
            await webSocket.CloseAsync(
                System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                "Closing connection",
                CancellationToken.None);
        }
    }
} 