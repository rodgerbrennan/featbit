using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Streaming.Connections;
using Streaming.Messages;
using Streaming.Metrics;

namespace Streaming;

/// <summary>
/// Streaming middleware: handle WebSocket upgrade and connection lifecycle.
/// Pre-accept validation flow:
/// 1. Validate request (HttpContext, query params).
/// 2. Always accept the WebSocket, then on failure close with a status code SDKs understand:
///    - Invalid (permanent): close with 4003 so SDKs stop reconnecting.
///    - Unavailable (transient): close with a non-4003 status (InternalServerError/1011) so SDKs reconnect.
/// 3. On Valid: accept WebSocket and process the connection.
/// 4. Close connection.
/// </summary>
public class StreamingMiddleware(
    IHostApplicationLifetime applicationLifetime,
    ILogger<StreamingMiddleware> logger,
    IStreamingMetrics metrics,
    RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IRequestValidator requestValidator,
        MessageDispatcher dispatcher,
        IConnectionManager connectionManager)
    {
        // if not streaming request
        if (!StreamingHelper.IsStreamingRequest(httpContext))
        {
            await next.Invoke(httpContext);
            return;
        }

        // Validate request PRE-accept (before accepting WebSocket)
        var validationResult = await requestValidator.ValidateAsync(httpContext);
        if (validationResult.Status == ValidationResultStatus.Invalid)
        {
            metrics.ConnectionRejected(validationResult.Reason);
            logger.RequestRejected(validationResult.Reason);

            // Protocol requirement: accept first, then close with 4003 so SDKs stop reconnecting.
            using var invalidWebSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
            await invalidWebSocket.CloseOutputAsync(
                (WebSocketCloseStatus)4003,
                "invalid request, close by server",
                CancellationToken.None
            );

            return;
        }

        if (validationResult.Status == ValidationResultStatus.Unavailable)
        {
            logger.RequestValidationUnavailable(validationResult.Reason, validationResult.Exception);

            // Transient server error (e.g. store unavailable). Protocol requirement: accept first, then
            // close with a non-4003 status so SDKs treat it as transient and reconnect.
            using var unavailableWebSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
            await unavailableWebSocket.CloseOutputAsync(
                WebSocketCloseStatus.InternalServerError,
                "service unavailable, close by server",
                CancellationToken.None
            );

            return;
        }

        // Validation passed; now accept the WebSocket
        using var websocket = await httpContext.WebSockets.AcceptWebSocketAsync();

        var connectionContext = new DefaultConnectionContext(websocket, httpContext);
        await connectionContext.PrepareForProcessingAsync(validationResult.Secrets);

        await connectionManager.Add(connectionContext);
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

            await connectionManager.Remove(connectionContext);
        }
    }
}
