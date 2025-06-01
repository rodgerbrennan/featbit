using System.Net.WebSockets;

namespace FeatBit.EvaluationServer.Shared.Models;

public class Connection
{
    public string Id { get; }
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
    }
}

public class Secret
{
    public string ProjectKey { get; set; } = string.Empty;
    public string EnvKey { get; set; } = string.Empty;
    public Guid EnvId { get; set; }
} 