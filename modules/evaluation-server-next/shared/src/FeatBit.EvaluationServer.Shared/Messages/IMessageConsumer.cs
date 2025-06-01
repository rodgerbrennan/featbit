namespace FeatBit.EvaluationServer.Shared.Messages;

public interface IMessageConsumer
{
    string Topic { get; }

    Task HandleAsync(string message, CancellationToken cancellationToken);
} 