using Application.Caches;
using Domain.Environments;

namespace Application.Environments;

public class DeleteSecret : IRequest<bool>
{
    public Guid EnvId { get; set; }

    public string SecretId { get; set; }
}

public class DeleteSecretHandler : IRequestHandler<DeleteSecret, bool>
{
    private readonly IEnvironmentService _service;
    private readonly ICacheService _cache;
    private readonly IPublisher _publisher;

    public DeleteSecretHandler(IEnvironmentService service, ICacheService cache, IPublisher publisher)
    {
        _service = service;
        _cache = cache;
        _publisher = publisher;
    }

    public async Task<bool> Handle(DeleteSecret request, CancellationToken cancellationToken)
    {
        var removedSecret = await _service.RemoveSecretAsync(request.EnvId, request.SecretId);
        if (removedSecret != null)
        {
            // publish on secret deleted notification
            await _publisher.Publish(new OnSecretDeleted(removedSecret), cancellationToken);
            return true;
        }

        // The secret is not in the environment row: it was already deleted, or a concurrent whole-row
        // write lost it before secret writes were atomic. A lost secret can still be in the cache, where
        // the evaluation server keeps accepting it, so remove every cache entry of this environment that
        // the row no longer holds. Read the cache before the row: a secret is cached only after it is
        // stored, so a concurrent add is always in the row we read.
        var cachedValues = await _cache.GetSecretValuesAsync(request.EnvId);
        if (cachedValues.Count == 0)
        {
            return true;
        }

        var environment = await _service.GetAsync(request.EnvId);
        var storedValues = environment.Secrets.Select(x => x.Value).ToHashSet();
        foreach (var strayValue in cachedValues.Where(x => !storedValues.Contains(x)))
        {
            await _publisher.Publish(new OnSecretDeleted(new Secret { Value = strayValue }), cancellationToken);
        }

        return true;
    }
}