using System.Collections.Concurrent;
using FeatBit.EvaluationServer.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Edge.WebSocket.Connections;

public sealed partial class ConnectionManager : IConnectionManager
{
    private readonly ILogger<ConnectionManager> _logger;
    internal readonly ConcurrentDictionary<string, Connection> Connections = new(StringComparer.Ordinal);

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    public void Add(ConnectionContext context)
    {
        if (context.Type == ConnectionType.RelayProxy)
        {
            foreach (var connection in context.MappedRpConnections)
            {
                Connections.TryAdd(connection.Id, connection);
            }
        }
        else
        {
            Connections.TryAdd(context.Connection.Id, context.Connection);
        }

        _logger.LogTrace("Connection added - Type: {Type}, Token: {Token}, Version: {Version}", 
            context.Type, context.Token, context.Version);
    }

    public void Remove(ConnectionContext context)
    {
        if (context.Type == ConnectionType.RelayProxy)
        {
            foreach (var mappedConnection in context.MappedRpConnections)
            {
                Connections.TryRemove(mappedConnection.Id, out _);
            }
        }
        else
        {
            Connections.TryRemove(context.Connection.Id, out _);
        }

        context.MarkAsClosed();

        _logger.LogTrace("Connection removed - Type: {Type}, Token: {Token}, Version: {Version}", 
            context.Type, context.Token, context.Version);
    }

    public ICollection<Connection> GetEnvConnections(Guid envId)
    {
        var connections = new List<Connection>();

        // the enumerator returned from the concurrent dictionary is safe to use concurrently with reads and writes to the dictionary
        // see https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getenumerator?view=net-6.0
        foreach (var entry in Connections)
        {
            var connection = entry.Value;
            if (connection.EnvId == envId)
            {
                connections.Add(connection);
            }
        }

        return connections;
    }
} 