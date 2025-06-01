using FeatBit.EvaluationServer.Broker.Domain.Messages;

namespace FeatBit.EvaluationServer.Broker.Core.Services;

public interface IMessageRoutingService
{
    Task RouteMessageAsync(IMessage message, CancellationToken cancellationToken = default);
    Task<bool> ValidateRouteAsync(string topic);
    Task SubscribeAsync(string topic, Func<IMessage, Task> handler);
    Task UnsubscribeAsync(string topic);
} 