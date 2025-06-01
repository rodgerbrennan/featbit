using System.Threading.Tasks;
using FeatBit.EvaluationServer.Hub.Domain.Models;

namespace FeatBit.EvaluationServer.Hub.Streaming.Messages;

public interface IMessageHandler
{
    string MessageType { get; }
    Task HandleAsync(ConnectionContext context, byte[] message);
} 