namespace FeatBit.EvaluationServer.Shared.Messaging;

public interface IMessageHandler
{
    string Topic { get; }
    Task HandleAsync(string message, CancellationToken cancellationToken);
} 