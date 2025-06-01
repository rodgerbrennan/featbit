using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.EvaluationServer.Edge.Domain.Interfaces;
using FeatBit.EvaluationServer.Edge.Domain.Models;
using FeatBit.EvaluationServer.Edge.WebSocket.Connections;
using FeatBit.EvaluationServer.Edge.WebSocket.Messages;
using FeatBit.EvaluationServer.Shared.Metrics;
using FeatBit.EvaluationServer.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IConnectionManager = FeatBit.EvaluationServer.Edge.Domain.Interfaces.IConnectionManager;
using DomainConnectionContext = FeatBit.EvaluationServer.Edge.Domain.Models.ConnectionContext;

namespace FeatBit.EvaluationServer.Edge.WebSocket;

public class StreamingMiddleware
{
    private const string StreamingPath = "/streaming";
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<StreamingMiddleware> _logger;
    private readonly IStreamingMetrics _metrics;
    private readonly RequestDelegate _next;
    private readonly IConnectionManager _connectionManager;

    public StreamingMiddleware(
        IHostApplicationLifetime applicationLifetime,
        ILogger<StreamingMiddleware> logger,
        IStreamingMetrics metrics,
        RequestDelegate next,
        IConnectionManager connectionManager)
    {
        _applicationLifetime = applicationLifetime;
        _logger = logger;
        _metrics = metrics;
        _next = next;
        _connectionManager = connectionManager;
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

    private async Task HandleWebSocketConnection(DomainConnectionContext context)
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