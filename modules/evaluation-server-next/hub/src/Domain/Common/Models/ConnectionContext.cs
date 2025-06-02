using System.Net.WebSockets;

namespace FeatBit.EvaluationServer.Hub.Domain.Common.Models;

public abstract class ConnectionContext : IAsyncDisposable
{
    public abstract string? RawQuery { get; protected init; }
    public abstract WebSocket WebSocket { get; protected init; }
    public abstract string Type { get; protected init; }
    public abstract string Version { get; protected init; }
    public abstract string Token { get; protected init; }
    public abstract Client? Client { get; protected set; }
    public abstract Connection Connection { get; protected init; }
    public abstract long ConnectAt { get; protected init; }
    public abstract long ClosedAt { get; protected set; }

    public string Id { get; set; } = string.Empty;
    public Guid EnvId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public IDictionary<string, object> UserAttributes { get; set; } = new Dictionary<string, object>();

    protected ConnectionContext(WebSocket websocket)
    {
        WebSocket = websocket;
    }

    public async Task CloseAsync()
    {
        var status = WebSocket.CloseStatus ?? WebSocketCloseStatus.NormalClosure;
        var description = WebSocket.CloseStatusDescription ?? string.Empty;

        if (WebSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await WebSocket.CloseOutputAsync(status, description, CancellationToken.None);
        }

        MarkAsClosed();
    }

    public void MarkAsClosed() => ClosedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task SendAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        => await WebSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);

    public void Deconstruct(
        out WebSocket websocket,
        out string type,
        out string version,
        out string token)
    {
        websocket = WebSocket;
        type = Type;
        version = Version;
        token = Token;
    }

    public abstract ValueTask DisposeAsync();
} 