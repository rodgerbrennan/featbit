using Application.Bases.Exceptions;
using Application.Caches;
using Application.Environments;
using Application.Services;
using Domain.Environments;
using MediatR;
using Environment = Domain.Environments.Environment;

namespace Application.UnitTests.Handlers;

public class SecretHandlerTests
{
    private readonly Mock<IEnvironmentService> _service = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IPublisher> _publisher = new();

    [Fact]
    public async Task AddSecret_StoreFails_DoesNotPublishOnSecretAdded()
    {
        var envId = Guid.NewGuid();
        _service
            .Setup(x => x.AddSecretAsync(envId, It.IsAny<Secret>()))
            .ThrowsAsync(new EntityNotFoundException(nameof(Environment), envId.ToString()));
        var sut = new AddSecretHandler(_service.Object, _publisher.Object);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => sut.Handle(
            new AddSecret { EnvId = envId, Name = "s", Type = SecretTypes.Server }, CancellationToken.None));

        _publisher.Verify(x => x.Publish(It.IsAny<OnSecretAdded>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddSecret_Stored_PublishesTheStoredSecret()
    {
        var envId = Guid.NewGuid();
        Secret? stored = null;
        _service
            .Setup(x => x.AddSecretAsync(envId, It.IsAny<Secret>()))
            .Callback((Guid _, Secret s) => stored = s)
            .Returns(Task.CompletedTask);
        var sut = new AddSecretHandler(_service.Object, _publisher.Object);

        var result = await sut.Handle(
            new AddSecret { EnvId = envId, Name = "s", Type = SecretTypes.Server }, CancellationToken.None);

        Assert.Same(stored, result);
        _publisher.Verify(x => x.Publish(
            It.Is<OnSecretAdded>(n => n.EnvId == envId && n.Secret == stored), It.IsAny<CancellationToken>()), Times.Once);
        _service.Verify(x => x.UpdateAsync(It.IsAny<Environment>()), Times.Never);
    }

    [Fact]
    public async Task DeleteSecret_Removed_PublishesOnSecretDeletedWithoutReadingCache()
    {
        var envId = Guid.NewGuid();
        var secret = new Secret(envId, "s", SecretTypes.Server);
        _service.Setup(x => x.RemoveSecretAsync(envId, secret.Id)).ReturnsAsync(secret);
        var sut = new DeleteSecretHandler(_service.Object, _cache.Object, _publisher.Object);

        Assert.True(await sut.Handle(new DeleteSecret { EnvId = envId, SecretId = secret.Id }, CancellationToken.None));

        _publisher.Verify(x => x.Publish(
            It.Is<OnSecretDeleted>(n => n.Secret == secret), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(x => x.GetSecretValuesAsync(It.IsAny<Guid>()), Times.Never);
        _service.Verify(x => x.UpdateAsync(It.IsAny<Environment>()), Times.Never);
    }

    [Fact]
    public async Task DeleteSecret_Absent_PublishesDeleteOnlyForCachedValuesMissingFromRow()
    {
        var env = new Environment(Guid.NewGuid(), "env", "env");
        var live = env.Secrets.First();
        var stray = new Secret(env.Id, "lost", SecretTypes.Server);
        _service.Setup(x => x.RemoveSecretAsync(env.Id, stray.Id)).ReturnsAsync((Secret?)null);
        _service.Setup(x => x.GetAsync(env.Id)).ReturnsAsync(env);
        _cache.Setup(x => x.GetSecretValuesAsync(env.Id)).ReturnsAsync([live.Value, stray.Value]);
        var sut = new DeleteSecretHandler(_service.Object, _cache.Object, _publisher.Object);

        Assert.True(await sut.Handle(new DeleteSecret { EnvId = env.Id, SecretId = stray.Id }, CancellationToken.None));

        _publisher.Verify(x => x.Publish(
            It.Is<OnSecretDeleted>(n => n.Secret.Value == stray.Value), It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(x => x.Publish(It.IsAny<OnSecretDeleted>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteSecret_AbsentAndNothingCached_ReturnsTrueWithoutPublishing()
    {
        var envId = Guid.NewGuid();
        _service.Setup(x => x.RemoveSecretAsync(envId, "missing")).ReturnsAsync((Secret?)null);
        _cache.Setup(x => x.GetSecretValuesAsync(envId)).ReturnsAsync(Array.Empty<string>());
        var sut = new DeleteSecretHandler(_service.Object, _cache.Object, _publisher.Object);

        Assert.True(await sut.Handle(new DeleteSecret { EnvId = envId, SecretId = "missing" }, CancellationToken.None));

        _publisher.Verify(x => x.Publish(It.IsAny<OnSecretDeleted>(), It.IsAny<CancellationToken>()), Times.Never);
        _service.Verify(x => x.GetAsync(It.IsAny<Guid>()), Times.Never);
    }
}
