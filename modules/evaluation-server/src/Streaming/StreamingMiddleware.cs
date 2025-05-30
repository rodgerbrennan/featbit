﻿using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Streaming.Connections;
using Streaming.Messages;
using Streaming.Metrics;

namespace Streaming;

public class StreamingMiddleware(
    IHostApplicationLifetime applicationLifetime,
    ILogger<StreamingMiddleware> logger,
    IStreamingMetrics metrics,
    RequestDelegate next)
{
    private const string StreamingPath = "/streaming";

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
            await next.Invoke(httpContext);
            return;
        }

        using var websocket = await httpContext.WebSockets.AcceptWebSocketAsync();

        var connectionContext = new DefaultConnectionContext(websocket, httpContext);
        var validationResult = await requestValidator.ValidateAsync(connectionContext);
        if (!validationResult.IsValid)
        {
            metrics.ConnectionRejected(validationResult.Reason);
            logger.RequestRejected(httpContext.Request.QueryString.Value, validationResult.Reason);
            await websocket.CloseOutputAsync(
                (WebSocketCloseStatus)4003,
                "invalid request, close by server",
                CancellationToken.None
            );
            return;
        }

        await connectionContext.PrepareForProcessingAsync(validationResult.Secrets);

        connectionManager.Add(connectionContext);
        metrics.ConnectionEstablished(connectionContext.Type);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            httpContext.RequestAborted,
            applicationLifetime.ApplicationStopping
        );

        try
        {
            // dispatch connection messages
            await dispatcher.DispatchAsync(connectionContext, cts.Token);
        }
        catch (WebSocketException ex)
        {
            metrics.ConnectionError(ex.WebSocketErrorCode.ToString());
            throw;
        }
        finally
        {
            // dispatch end means the connection was closed
            await connectionContext.CloseAsync();
            
            var duration = connectionContext.ClosedAt - connectionContext.ConnectAt;
            metrics.ConnectionClosed(duration);
            
            connectionManager.Remove(connectionContext);
        }
    }
}