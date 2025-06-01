using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.EvaluationServer.Shared.Models;

namespace FeatBit.EvaluationServer.Edge.Domain.Models;

public abstract class ConnectionContext : IAsyncDisposable
{
    public virtual string? RawQuery { get; protected init; }
    public virtual WebSocket WebSocket { get; }
    public virtual string Type { get; protected init; } = string.Empty;
    public virtual string Version { get; protected init; } = string.Empty;
    public virtual string Token { get; protected init; } = string.Empty;
    public virtual Client? Client { get; protected set; }
    public virtual Connection Connection { get; protected init; } = null!;
    public virtual Connection[] MappedRpConnections { get; protected set; }
    public virtual long ConnectAt { get; protected init; }
    public virtual long ClosedAt { get; protected set; }

    protected ConnectionContext(WebSocket websocket)
    {
        WebSocket = websocket;
        MappedRpConnections = [];
    }

    public abstract ValueTask DisposeAsync();
} 