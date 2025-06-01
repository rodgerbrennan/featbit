using System.Threading.Tasks;
using FeatBit.EvaluationServer.Edge.Domain.Models;

namespace FeatBit.EvaluationServer.Edge.Domain.Interfaces;

public interface IMessageHandler
{
    string MessageType { get; }
    Task HandleAsync(ConnectionContext context, byte[] message);
} 