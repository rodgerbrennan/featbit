using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FeatBit.EvaluationServer.Hub.Domain.Models;

public class ConnectionContext : IAsyncDisposable
{
    public Guid EnvId { get; }
    public WebSocket WebSocket { get; }
    private readonly CancellationTokenSource _cts;

    public ConnectionContext(Guid envId, WebSocket webSocket)
    {
        EnvId = envId;
        WebSocket = webSocket;
        _cts = new CancellationTokenSource();
    }

    public CancellationToken ConnectionClosed => _cts.Token;

    public void MarkAsDisconnected()
    {
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
    }

    public async Task SendAsync<T>(string messageType, T payload)
    {
        if (WebSocket.State != WebSocketState.Open)
        {
            return;
        }

        var message = new
        {
            Type = messageType,
            Data = payload
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(message);
        await WebSocket.SendAsync(json, WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        
        if (WebSocket.State == WebSocketState.Open)
        {
            await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
        }
        
        WebSocket.Dispose();
    }
} 