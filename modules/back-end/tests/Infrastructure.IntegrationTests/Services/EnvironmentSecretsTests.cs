using Application.Caches;
using Application.Environments;
using Application.Services;
using Domain.Environments;
using Domain.Messages;
using Domain.Organizations;
using Domain.Projects;
using Infrastructure.Caches.Redis;
using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Persistence.MongoDb;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Environment = Domain.Environments.Environment;
using EfEnvironmentService = Infrastructure.Services.EntityFrameworkCore.EnvironmentService;
using MongoEnvironmentService = Infrastructure.Services.MongoDb.EnvironmentService;

namespace Infrastructure.IntegrationTests.Services;

/// <summary>
/// Change 5 (atomic secret add and delete). The same scenarios run against Postgres and MongoDB:
/// concurrent secret writes must never lose a secret, and a delete must always clean the cache.
/// </summary>
public abstract class EnvironmentSecretsTestsBase : IntegrationTestBase, IAsyncLifetime
{
    private const int Concurrency = 20;

    private readonly RedisFixture _redis;
    private ConnectionMultiplexer _connection = null!;

    protected EnvironmentSecretsTestsBase(RedisFixture redis)
    {
        _redis = redis;
    }

    /// <summary>A fresh service instance (for EF, a fresh DbContext), safe to use from one task.</summary>
    protected abstract IEnvironmentService NewService();

    /// <summary>Seeds an organization, project and environment and returns the environment.</summary>
    protected abstract Task<Environment> SeedEnvAsync();

    public async Task InitializeAsync()
    {
        if (!DockerAvailability.IsAvailable)
        {
            return;
        }

        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.ConnectionString);
    }

    public Task DisposeAsync()
    {
        _connection?.Dispose();
        return Task.CompletedTask;
    }

    private ICacheService NewCache()
    {
        var client = new Mock<IRedisClient>();
        client.Setup(x => x.GetDatabase()).Returns(() => _connection.GetDatabase());
        client.Setup(x => x.Connection).Returns(_connection);
        return new RedisCacheService(client.Object);
    }

    private async Task<bool> InRedisAsync(Secret secret) =>
        await _connection.GetDatabase().KeyExistsAsync(RedisKeys.Secret(secret.Value));

    private async Task<Environment> ReloadAsync(Guid envId) => await NewService().GetAsync(envId);

    // ---- service level ---------------------------------------------------------------------------

    [DockerFact]
    public async Task AddSecretAsync_ConcurrentAdds_AllSecretsPersist()
    {
        var env = await SeedEnvAsync();
        var secrets = Enumerable.Range(0, Concurrency)
            .Select(i => new Secret(env.Id, $"s-{i}", SecretTypes.Server))
            .ToArray();

        await Task.WhenAll(secrets.Select(s => Task.Run(() => NewService().AddSecretAsync(env.Id, s))));

        var reloaded = await ReloadAsync(env.Id);
        Assert.Equal(env.Secrets.Count + Concurrency, reloaded.Secrets.Count);
        Assert.All(secrets, s => Assert.Contains(reloaded.Secrets, x => x.Id == s.Id && x.Value == s.Value));
    }

    [DockerFact]
    public async Task AddSecretAsync_UnknownEnvironment_Throws()
    {
        var envId = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<Exception>(
            () => NewService().AddSecretAsync(envId, new Secret(envId, "s", SecretTypes.Server)));
    }

    [DockerFact]
    public async Task RemoveSecretAsync_ExistingSecret_ReturnsItAndRemovesOnlyIt()
    {
        var env = await SeedEnvAsync();
        var target = env.Secrets.First();

        var removed = await NewService().RemoveSecretAsync(env.Id, target.Id);

        Assert.NotNull(removed);
        Assert.Equal(target.Id, removed!.Id);
        Assert.Equal(target.Value, removed.Value);
        Assert.Equal(target.Type, removed.Type);
        var reloaded = await ReloadAsync(env.Id);
        Assert.DoesNotContain(reloaded.Secrets, x => x.Id == target.Id);
        Assert.Equal(env.Secrets.Count - 1, reloaded.Secrets.Count);
    }

    [DockerFact]
    public async Task RemoveSecretAsync_LastSecret_LeavesEmptyList()
    {
        var env = await SeedEnvAsync();
        foreach (var secret in env.Secrets)
        {
            await NewService().RemoveSecretAsync(env.Id, secret.Id);
        }

        var reloaded = await ReloadAsync(env.Id);
        Assert.NotNull(reloaded.Secrets);
        Assert.Empty(reloaded.Secrets);
    }

    [DockerFact]
    public async Task RemoveSecretAsync_AbsentSecret_ReturnsNullAndLeavesRow()
    {
        var env = await SeedEnvAsync();

        var removed = await NewService().RemoveSecretAsync(env.Id, Guid.NewGuid().ToString("D"));

        Assert.Null(removed);
        Assert.Equal(env.Secrets.Count, (await ReloadAsync(env.Id)).Secrets.Count);
    }

    [DockerFact]
    public async Task RemoveSecretAsync_ConcurrentWithAdds_RemovesOnlyTarget()
    {
        var env = await SeedEnvAsync();
        var target = env.Secrets.First();
        var added = Enumerable.Range(0, Concurrency)
            .Select(i => new Secret(env.Id, $"s-{i}", SecretTypes.Server))
            .ToArray();

        var tasks = added
            .Select(s => Task.Run(() => NewService().AddSecretAsync(env.Id, s)))
            .Append(Task.Run(async () => Assert.NotNull(await NewService().RemoveSecretAsync(env.Id, target.Id))));
        await Task.WhenAll(tasks);

        var reloaded = await ReloadAsync(env.Id);
        Assert.DoesNotContain(reloaded.Secrets, x => x.Id == target.Id);
        Assert.All(added, s => Assert.Contains(reloaded.Secrets, x => x.Id == s.Id));
        Assert.Equal(env.Secrets.Count - 1 + Concurrency, reloaded.Secrets.Count);
    }

    [DockerFact]
    public async Task UpdateAsync_StaleEnvironment_DoesNotOverwriteSecrets()
    {
        var env = await SeedEnvAsync();
        var editor = NewService();
        var stale = await editor.GetAsync(env.Id);

        // a secret is added after the editor loaded the environment
        var added = new Secret(env.Id, "added-later", SecretTypes.Server);
        await NewService().AddSecretAsync(env.Id, added);

        stale.Update("renamed", "new description");
        await editor.UpdateAsync(stale);

        var reloaded = await ReloadAsync(env.Id);
        Assert.Equal("renamed", reloaded.Name);
        Assert.Equal("new description", reloaded.Description);
        Assert.Equal(env.ProjectId, reloaded.ProjectId);
        Assert.Equal(env.Key, reloaded.Key);
        Assert.Contains(reloaded.Secrets, x => x.Id == added.Id);
        Assert.Equal(env.Secrets.Count + 1, reloaded.Secrets.Count);
    }

    [DockerFact]
    public async Task UpdateSecretNameAsync_RenamesOnlyThatSecret()
    {
        var env = await SeedEnvAsync();
        var target = env.Secrets.First();
        var other = env.Secrets.Last();

        // a concurrent add must survive the rename
        var added = new Secret(env.Id, "added", SecretTypes.Client);
        await NewService().AddSecretAsync(env.Id, added);

        await NewService().UpdateSecretNameAsync(env.Id, target.Id, "renamed");

        var reloaded = await ReloadAsync(env.Id);
        var renamed = Assert.Single(reloaded.Secrets, x => x.Id == target.Id);
        Assert.Equal("renamed", renamed.Name);
        Assert.Equal(target.Value, renamed.Value);
        Assert.Equal(target.Type, renamed.Type);
        Assert.Equal(other.Name, Assert.Single(reloaded.Secrets, x => x.Id == other.Id).Name);
        Assert.Contains(reloaded.Secrets, x => x.Id == added.Id);
    }

    // ---- handler level: row and Redis must agree --------------------------------------------------

    private IPublisher NewPublisher(IEnvironmentService service)
    {
        var cache = NewCache();
        var configuration = new ConfigurationBuilder().Build();
        var producer = new Mock<IMessageProducer>().Object;
        var added = new OnSecretAddedHandler(cache, service, configuration, producer);
        var deleted = new OnSecretDeletedHandler(cache, configuration, producer);

        var publisher = new Mock<IPublisher>();
        publisher
            .Setup(x => x.Publish(It.IsAny<OnSecretAdded>(), It.IsAny<CancellationToken>()))
            .Returns((OnSecretAdded n, CancellationToken ct) => added.Handle(n, ct));
        publisher
            .Setup(x => x.Publish(It.IsAny<OnSecretDeleted>(), It.IsAny<CancellationToken>()))
            .Returns((OnSecretDeleted n, CancellationToken ct) => deleted.Handle(n, ct));
        return publisher.Object;
    }

    private async Task<Secret> AddViaHandlerAsync(Guid envId, string name)
    {
        var service = NewService();
        var handler = new AddSecretHandler(service, NewPublisher(service));
        return await handler.Handle(
            new AddSecret { EnvId = envId, Name = name, Type = SecretTypes.Server }, CancellationToken.None);
    }

    private async Task<bool> DeleteViaHandlerAsync(Guid envId, string secretId)
    {
        var service = NewService();
        var handler = new DeleteSecretHandler(service, NewCache(), NewPublisher(service));
        return await handler.Handle(new DeleteSecret { EnvId = envId, SecretId = secretId }, CancellationToken.None);
    }

    [DockerFact]
    public async Task AddSecretHandler_ConcurrentAdds_AllSecretsInRowAndRedis()
    {
        var env = await SeedEnvAsync();

        var secrets = await Task.WhenAll(Enumerable.Range(0, Concurrency)
            .Select(i => Task.Run(() => AddViaHandlerAsync(env.Id, $"s-{i}"))));

        var reloaded = await ReloadAsync(env.Id);
        foreach (var secret in secrets)
        {
            Assert.Contains(reloaded.Secrets, x => x.Id == secret.Id);
            Assert.True(await InRedisAsync(secret));
        }
    }

    [DockerFact]
    public async Task Handlers_ConcurrentAddAndDelete_RowAndRedisAgree()
    {
        var env = await SeedEnvAsync();
        var toDelete = await Task.WhenAll(Enumerable.Range(0, Concurrency / 2)
            .Select(i => AddViaHandlerAsync(env.Id, $"old-{i}")));

        var adds = Enumerable.Range(0, Concurrency / 2)
            .Select(i => Task.Run(() => AddViaHandlerAsync(env.Id, $"new-{i}")))
            .ToArray();
        var deletes = toDelete
            .Select(s => Task.Run(() => DeleteViaHandlerAsync(env.Id, s.Id)))
            .ToArray();
        await Task.WhenAll(adds.Cast<Task>().Concat(deletes));

        var reloaded = await ReloadAsync(env.Id);
        Assert.All(deletes, d => Assert.True(d.Result));
        foreach (var secret in toDelete)
        {
            Assert.DoesNotContain(reloaded.Secrets, x => x.Id == secret.Id);
            Assert.False(await InRedisAsync(secret));
        }

        foreach (var secret in adds.Select(a => a.Result))
        {
            Assert.Contains(reloaded.Secrets, x => x.Id == secret.Id);
            Assert.True(await InRedisAsync(secret));
        }
    }

    [DockerFact]
    public async Task DeleteSecretHandler_AbsentId_KeepsSecretAddedAfterEnvWasLoadedInScope()
    {
        var env = await SeedEnvAsync();

        // the request scope already loaded the environment before another request adds a secret; the stray
        // check must read the row fresh (production EF uses NoTracking), or it would delete the new secret
        var scoped = NewService();
        await scoped.GetAsync(env.Id);
        var added = await AddViaHandlerAsync(env.Id, "added-elsewhere");

        var handler = new DeleteSecretHandler(scoped, NewCache(), NewPublisher(scoped));
        Assert.True(await handler.Handle(
            new DeleteSecret { EnvId = env.Id, SecretId = Guid.NewGuid().ToString("D") }, CancellationToken.None));

        Assert.True(await InRedisAsync(added));
    }

    [DockerFact]
    public async Task AddSecretAsync_NullSecretsField_StartsTheList()
    {
        var env = await SeedEnvAsync();
        await SetSecretsNullAsync(env.Id);
        var secret = new Secret(env.Id, "first", SecretTypes.Server);

        await NewService().AddSecretAsync(env.Id, secret);

        Assert.Single((await ReloadAsync(env.Id)).Secrets, x => x.Id == secret.Id);
    }

    /// <summary>Simulates a legacy row whose secrets field is null.</summary>
    protected abstract Task SetSecretsNullAsync(Guid envId);

    [DockerFact]
    public async Task DeleteSecretHandler_AbsentId_ReturnsTrueAndRemovesStrayRedisEntry()
    {
        var env = await SeedEnvAsync();
        var otherEnv = await SeedEnvAsync();
        var cache = NewCache();
        var descriptor = (await NewService().GetResourceDescriptorAsync(env.Id))!;
        var otherDescriptor = (await NewService().GetResourceDescriptorAsync(otherEnv.Id))!;

        // live secrets are cached, as OnSecretAdded / OnEnvironmentAdded would have done
        foreach (var secret in env.Secrets)
        {
            await cache.UpsertSecretAsync(descriptor, secret);
        }

        // a secret lost from the row before this fix: still in Redis, no longer in the row
        var stray = new Secret(env.Id, "lost", SecretTypes.Server);
        await cache.UpsertSecretAsync(descriptor, stray);

        // another environment's cached secret must not be touched
        var foreign = otherEnv.Secrets.First();
        await cache.UpsertSecretAsync(otherDescriptor, foreign);

        var result = await DeleteViaHandlerAsync(env.Id, stray.Id);

        Assert.True(result);
        Assert.False(await InRedisAsync(stray));
        foreach (var secret in env.Secrets)
        {
            Assert.True(await InRedisAsync(secret));
        }

        Assert.True(await InRedisAsync(foreign));
        Assert.Equal(env.Secrets.Count, (await ReloadAsync(env.Id)).Secrets.Count);
    }
}

[Collection(FeatBitPostgresCollection.Name)]
public sealed class PostgresEnvironmentSecretsTests : EnvironmentSecretsTestsBase, IClassFixture<RedisFixture>
{
    private readonly FeatBitPostgresFixture _postgres;

    public PostgresEnvironmentSecretsTests(FeatBitPostgresFixture postgres, RedisFixture redis) : base(redis)
    {
        _postgres = postgres;
    }

    protected override IEnvironmentService NewService() =>
        new EfEnvironmentService(
            AppDbContextFactory.Create(_postgres.ConnectionString),
            NullLogger<EfEnvironmentService>.Instance);

    protected override async Task<Environment> SeedEnvAsync()
    {
        await using var db = AppDbContextFactory.Create(_postgres.ConnectionString);
        var (org, project, env) = NewHierarchy();
        db.Set<Organization>().Add(org);
        db.Set<Project>().Add(project);
        db.Set<Environment>().Add(env);
        await db.SaveChangesAsync();
        return env;
    }

    protected override async Task SetSecretsNullAsync(Guid envId)
    {
        await using var db = AppDbContextFactory.Create(_postgres.ConnectionString);
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            db.Database, $"UPDATE environments SET secrets = NULL WHERE id = {envId}");
    }

    internal static (Organization, Project, Environment) NewHierarchy()
    {
        var org = new Organization(Guid.NewGuid(), $"org-{Guid.NewGuid():N}", $"org-{Guid.NewGuid():N}")
        {
            Id = Guid.NewGuid()
        };
        var project = new Project(org.Id, "proj", $"proj-{Guid.NewGuid():N}") { Id = Guid.NewGuid() };
        var env = new Environment(project.Id, "env", $"env-{Guid.NewGuid():N}");
        return (org, project, env);
    }
}

[Collection(MongoCollection.Name)]
public sealed class MongoEnvironmentSecretsTests : EnvironmentSecretsTestsBase, IClassFixture<RedisFixture>
{
    private readonly MongoDbFixture _mongo;
    private readonly string _database = $"be-env-secrets-{Guid.NewGuid():N}";

    public MongoEnvironmentSecretsTests(MongoDbFixture mongo, RedisFixture redis) : base(redis)
    {
        _mongo = mongo;
    }

    private MongoDbClient NewClient() => new(Options.Create(new MongoDbOptions
    {
        ConnectionString = _mongo.ConnectionString,
        Database = _database
    }));

    protected override IEnvironmentService NewService() =>
        new MongoEnvironmentService(NewClient(), NullLogger<MongoEnvironmentService>.Instance);

    protected override async Task<Environment> SeedEnvAsync()
    {
        var client = NewClient();
        var (org, project, env) = PostgresEnvironmentSecretsTests.NewHierarchy();
        await client.CollectionOf<Organization>().InsertOneAsync(org);
        await client.CollectionOf<Project>().InsertOneAsync(project);
        await client.CollectionOf<Environment>().InsertOneAsync(env);
        return env;
    }

    protected override async Task SetSecretsNullAsync(Guid envId) =>
        await NewClient().CollectionOf<Environment>().UpdateOneAsync(
            MongoDB.Driver.Builders<Environment>.Filter.Eq(x => x.Id, envId),
            MongoDB.Driver.Builders<Environment>.Update.Set(x => x.Secrets, null));
}
