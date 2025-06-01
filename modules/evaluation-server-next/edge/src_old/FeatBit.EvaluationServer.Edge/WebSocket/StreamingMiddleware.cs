using System.Net.WebSockets;
using FeatBit.EvaluationServer.Edge.WebSocket.Connections;
using FeatBit.EvaluationServer.Edge.WebSocket.Messages;
using FeatBit.EvaluationServer.Shared.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Edge.WebSocket;

public class StreamingMiddleware
{
    private const string StreamingPath = "/streaming";
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<StreamingMiddleware> _logger;
    private readonly IStreamingMetrics _metrics;
    private readonly RequestDelegate _next;

    public StreamingMiddleware(
        IHostApplicationLifetime applicationLifetime,
        ILogger<StreamingMiddleware> logger,
        IStreamingMetrics metrics,
        RequestDelegate next)
    {
        _applicationLifetime = applicationLifetime;
        _logger = logger;
        _metrics = metrics;
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        IRequestValidator requestValidator,
        MessageDispatcher dispatcher,
        IConnectionManager connectionManager)
    {
        var request = httpContext.Request;

        // if not streaming request
        if (!request.Path.StartsWithSegments(StreamingPath) || !httpContext.WebSockets.IsWebSocketRequest)
        {
            await _next.Invoke(httpContext);
            return;
        }

        using var websocket = await httpContext.WebSockets.AcceptWebSocketAsync();

        var connectionContext = new DefaultConnectionContext(websocket, httpContext);
        var validationResult = await requestValidator.ValidateAsync(connectionContext);
        if (!validationResult.IsValid)
        {
            _metrics.ConnectionRejected(validationResult.Reason);
            _logger.LogWarning("Request rejected: {Query}, Reason: {Reason}", 
                httpContext.Request.QueryString.Value, validationResult.Reason);
            await websocket.CloseOutputAsync(
                (WebSocketCloseStatus)4003,
                "invalid request, close by server",
                CancellationToken.None
            );
            return;
        }

        await connectionContext.PrepareForProcessingAsync(validationResult.Secrets);

        connectionManager.Add(connectionContext);
        _metrics.ConnectionEstablished(connectionContext.Type);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            httpContext.RequestAborted,
            _applicationLifetime.ApplicationStopping
        );

        try
        {
            // dispatch connection messages
            await dispatcher.DispatchAsync(connectionContext, cts.Token);
        }
        catch (WebSocketException ex)
        {
            _metrics.ConnectionError(ex.WebSocketErrorCode.ToString());
            throw;
        }
        finally
        {
            // dispatch end means the connection was closed
            await connectionContext.CloseAsync();
            
            var duration = connectionContext.ClosedAt - connectionContext.ConnectAt;
            _metrics.ConnectionClosed(duration);
            
            connectionManager.Remove(connectionContext);
        }
    }
} 