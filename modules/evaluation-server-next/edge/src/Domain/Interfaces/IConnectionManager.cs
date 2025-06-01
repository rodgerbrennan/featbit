using System.Collections.Generic;
using System.Threading.Tasks;
using FeatBit.EvaluationServer.Edge.Domain.Models;

namespace FeatBit.EvaluationServer.Edge.Domain.Interfaces;

public interface IConnectionManager
{
    Task Add(ConnectionContext context);
    Task Remove(string connectionId);
    IEnumerable<ConnectionContext> GetEnvConnections(string envId);
    ConnectionContext? GetConnection(string connectionId);
    IEnumerable<ConnectionContext> GetAllConnections();
    bool TryGetConnection(string connectionId, out ConnectionContext? context);
    Task DisconnectAllAsync();
} 