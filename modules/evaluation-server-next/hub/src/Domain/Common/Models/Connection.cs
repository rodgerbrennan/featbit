using System.Net.WebSockets;

namespace FeatBit.EvaluationServer.Hub.Domain.Common.Models;

public class Connection
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public long ConnectAt { get; set; }
    public long ClosedAt { get; set; }

    public WebSocket WebSocket { get; }
    public string ProjectKey { get; }
    public string EnvKey { get; }
    public Guid EnvId { get; }

    public Connection(WebSocket webSocket, Secret secret)
    {
        WebSocket = webSocket;
        ProjectKey = secret.ProjectKey;
        EnvKey = secret.EnvKey;
        EnvId = secret.EnvId;
        Id = $"{ProjectKey}:{EnvKey}";
        ConnectAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
} 