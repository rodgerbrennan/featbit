using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using FeatBit.EvaluationServer.Edge.Domain.Common.Models;
using FeatBit.EvaluationServer.Edge.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Edge.Infrastructure;

public partial class ConnectionManager : IConnectionManager
{
    private readonly ILogger<ConnectionManager> _logger;
    private readonly ConcurrentDictionary<string, ConnectionContext> _connections;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
        _connections = new ConcurrentDictionary<string, ConnectionContext>();
    }

    public Task Add(ConnectionContext context)
    {
        if (_connections.TryAdd(context.Connection.Id, context))
        {
            _logger.LogInformation("Added connection {ConnectionId}", context.Connection.Id);
        }
        return Task.CompletedTask;
    }

    public async Task Remove(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var context))
        {
            await context.DisposeAsync();
            _logger.LogInformation("Removed connection {ConnectionId}", connectionId);
        }
    }

    public ConnectionContext? GetConnection(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var context) ? context : null;
    }

    public bool TryGetConnection(string connectionId, out ConnectionContext? context)
    {
        return _connections.TryGetValue(connectionId, out context);
    }

    public IEnumerable<ConnectionContext> GetAllConnections()
    {
        return _connections.Values;
    }

    public IEnumerable<ConnectionContext> GetEnvConnections(string envId)
    {
        var envGuid = Guid.Parse(envId);
        return _connections.Values.Where(ctx => ctx.Connection.EnvId == envGuid);
    }

    public async Task DisconnectAllAsync()
    {
        foreach (var context in _connections.Values)
        {
            await Remove(context.Connection.Id);
        }
    }
} 