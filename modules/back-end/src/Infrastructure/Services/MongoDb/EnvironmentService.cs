using Application.Bases.Exceptions;
using Domain.EndUsers;
using Domain.Environments;
using Domain.Organizations;
using Domain.Projects;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Environment = Domain.Environments.Environment;

namespace Infrastructure.Services.MongoDb;

public class EnvironmentService(MongoDbClient mongoDb, ILogger<EnvironmentService> logger)
    : MongoDbService<Environment>(mongoDb), IEnvironmentService
{
    public async Task<Organization> GetOrganizationAsync(Guid envId)
    {
        var query = from environment in MongoDb.QueryableOf<Environment>()
            join project in MongoDb.QueryableOf<Project>() on environment.ProjectId equals project.Id
            join organization in MongoDb.QueryableOf<Organization>() on project.OrganizationId equals organization.Id
            where environment.Id == envId
            select organization;
        return await query.SingleAsync();
    }

    public async Task<string[]> GetServesAsync(string[] scopes)
    {
        if (scopes.Length == 0)
        {
            return [];
        }

        var projects = MongoDb.QueryableOf<Project>();
        var environments = MongoDb.QueryableOf<Environment>();

        var envIds = scopes.Select(Guid.Parse).ToArray();
        var query =
            from environment in environments
            join project in projects on environment.ProjectId equals project.Id
            where envIds.Contains(environment.Id)
            select new
            {
                envId = environment.Id,
                projectName = project.Name,
                envName = environment.Name
            };

        var result = await query.ToListAsync();
        return result
            .Select(x => $"{x.envId},{x.projectName}/{x.envName}")
            .ToArray();
    }

    public async Task<RpSecret[]> GetRpSecretsAsync(Guid[] envIds)
    {
        if (envIds.Length == 0)
        {
            return [];
        }

        var projects = MongoDb.QueryableOf<Project>();
        var environments = MongoDb.QueryableOf<Environment>();

        var query = from environment in environments
            join project in projects on environment.ProjectId equals project.Id
            where envIds.Contains(environment.Id)
            select new
            {
                EnvId = environment.Id,
                EnvKey = environment.Key,
                ProjectKey = project.Key,
                Secrets = environment.Secrets,
            };

        var result = await query.ToListAsync();

        var rpSecrets = result.Select(x => x.Secrets.Select(secret => new RpSecret
            {
                EnvId = x.EnvId,
                EnvKey = x.EnvKey,
                ProjectKey = x.ProjectKey,
                Type = secret.Type,
                Value = secret.Value
            }))
            .SelectMany(x => x)
            .ToArray();

        return rpSecrets;
    }

    public async Task<ResourceDescriptor?> GetResourceDescriptorAsync(Guid envId)
    {
        var organizations = MongoDb.QueryableOf<Organization>();
        var projects = MongoDb.QueryableOf<Project>();
        var environments = MongoDb.QueryableOf<Environment>();

        var query = from environment in environments
            join project in projects on environment.ProjectId equals project.Id
            join organization in organizations on project.OrganizationId equals organization.Id
            where environment.Id == envId
            select new ResourceDescriptor
            {
                Organization = new IdNameKeyProps
                {
                    Id = organization.Id,
                    Name = organization.Name,
                    Key = organization.Key
                },
                Project = new IdNameKeyProps
                {
                    Id = project.Id,
                    Name = project.Name,
                    Key = project.Key
                },
                Environment = new IdNameKeyProps
                {
                    Id = environment.Id,
                    Name = environment.Name,
                    Key = environment.Key
                }
            };

        var descriptor = await query.FirstOrDefaultAsync();
        return descriptor;
    }

    public async Task<ICollection<SecretCache>> GetSecretCachesAsync()
    {
        var organizations = MongoDb.QueryableOf<Organization>();
        var projects = MongoDb.QueryableOf<Project>();
        var environments = MongoDb.QueryableOf<Environment>();

        var descriptors = from environment in environments
            join project in projects on environment.ProjectId equals project.Id
            join organization in organizations on project.OrganizationId equals organization.Id
            select new ResourceDescriptor
            {
                Organization = new IdNameKeyProps
                {
                    Id = organization.Id,
                    Name = organization.Name,
                    Key = organization.Key
                },
                Project = new IdNameKeyProps
                {
                    Id = project.Id,
                    Name = project.Name,
                    Key = project.Key
                },
                Environment = new IdNameKeyProps
                {
                    Id = environment.Id,
                    Name = environment.Name,
                    Key = environment.Key
                }
            };

        var rds = await descriptors.ToListAsync();
        var envs = await environments.ToListAsync();

        var caches = new List<SecretCache>();
        var descriptorDictionary = rds.ToDictionary(x => x.Environment.Id, x => x);

        foreach (var env in envs)
        {
            if (descriptorDictionary.TryGetValue(env.Id, out var descriptor))
            {
                var secretCaches = env.Secrets.Select(x => new SecretCache(descriptor, x));
                caches.AddRange(secretCaches);
            }
            else
            {
                logger.LogWarning(
                    "Data inconsistency detected: Resource descriptor not found for environment with ID {EnvId}. Please verify the integrity of the environment data in the database.",
                    env.Id
                );
            }
        }

        return caches;
    }

    public async Task AddWithBuiltInPropsAsync(Environment env)
    {
        await Collection.InsertOneAsync(env);

        // add end-user built-in properties
        var builtInProperties = EndUserConsts.BuiltInUserProperties(env.Id);
        await MongoDb.CollectionOf<EndUserProperty>().InsertManyAsync(builtInProperties);
    }

    public async Task AddManyWithBuiltInPropsAsync(ICollection<Environment> envs)
    {
        await Collection.InsertManyAsync(envs);

        // add end-user built-in properties
        var builtInProperties = envs.SelectMany(x => EndUserConsts.BuiltInUserProperties(x.Id));
        await MongoDb.CollectionOf<EndUserProperty>().InsertManyAsync(builtInProperties);
    }

    public async Task DeleteAsync(Guid id)
    {
        await MongoDb.CollectionOf<Environment>().DeleteOneAsync(x => x.Id == id);

        // delete end users
        await MongoDb.CollectionOf<EndUser>().DeleteManyAsync(x => x.EnvId == id);

        // delete end user properties
        await MongoDb.CollectionOf<EndUserProperty>().DeleteManyAsync(x => x.EnvId == id);
    }

    public async Task DeleteManyAsync(ICollection<Guid> ids)
    {
        await Collection.DeleteManyAsync(x => ids.Contains(x.Id));

        // delete end users
        await MongoDb.CollectionOf<EndUser>().DeleteManyAsync(x => x.EnvId != null && ids.Contains(x.EnvId.Value));

        // delete end user properties
        await MongoDb.CollectionOf<EndUserProperty>().DeleteManyAsync(x => ids.Contains(x.EnvId));
    }

    public async Task<bool> HasKeyBeenUsedAsync(Guid projectId, string key)
    {
        return await AnyAsync(environment =>
            environment.ProjectId == projectId &&
            string.Equals(environment.Key, key, StringComparison.OrdinalIgnoreCase)
        );
    }

    public async Task<string?> GetProjectEnvAsync(Guid envId)
    {
        var query =
            from project in MongoDb.QueryableOf<Project>()
            join environment in MongoDb.QueryableOf<Environment>() on project.Id equals environment.ProjectId
            where environment.Id == envId
            select new
            {
                project = project.Key,
                env = environment.Key
            };

        var result = await query.FirstOrDefaultAsync();
        return result == null
            ? string.Empty
            : $"{result.project}/{result.env}";
    }


    private static readonly string SecretsElementName =
        BsonClassMap.LookupClassMap(typeof(Environment)).GetMemberMap(nameof(Environment.Secrets)).ElementName;

    /// <summary>
    /// Writes every field except <c>secrets</c>. Secrets change only through the targeted
    /// <see cref="AddSecretAsync"/>, <see cref="RemoveSecretAsync"/> and <see cref="UpdateSecretNameAsync"/>,
    /// so a stale copy of the environment can never overwrite a concurrent secret change.
    /// </summary>
    public override async Task UpdateAsync(Environment replacement)
    {
        var fields = replacement.ToBsonDocument();
        fields.Remove("_id");
        fields.Remove(SecretsElementName);

        await Collection.UpdateOneAsync(x => x.Id == replacement.Id, new BsonDocument("$set", fields));
    }

    public async Task AddSecretAsync(Guid envId, Secret secret)
    {
        // $push fails on a null field; start the list atomically in that case, as COALESCE does in Postgres
        var startList = await Collection.UpdateOneAsync(
            x => x.Id == envId && x.Secrets == null,
            Builders<Environment>.Update.Set(x => x.Secrets, new List<Secret> { secret }));
        if (startList.ModifiedCount == 1)
        {
            return;
        }

        var update = Builders<Environment>.Update.Push(x => x.Secrets, secret);

        var result = await Collection.UpdateOneAsync(x => x.Id == envId, update);
        if (result.MatchedCount == 0)
        {
            throw new EntityNotFoundException(nameof(Environment), envId.ToString());
        }
    }

    public async Task<Secret?> RemoveSecretAsync(Guid envId, string secretId)
    {
        var filter = Builders<Environment>.Filter.Eq(x => x.Id, envId) &
                     Builders<Environment>.Filter.ElemMatch(x => x.Secrets, s => s.Id == secretId);
        var update = Builders<Environment>.Update.PullFilter(x => x.Secrets, s => s.Id == secretId);
        var options = new FindOneAndUpdateOptions<Environment> { ReturnDocument = ReturnDocument.Before };

        var before = await Collection.FindOneAndUpdateAsync(filter, update, options);
        if (before != null)
        {
            return before.Secrets.First(s => s.Id == secretId);
        }

        await EnsureExistsAsync(envId);
        return null;
    }

    public async Task UpdateSecretNameAsync(Guid envId, string secretId, string name)
    {
        var filter = Builders<Environment>.Filter.Eq(x => x.Id, envId) &
                     Builders<Environment>.Filter.ElemMatch(x => x.Secrets, s => s.Id == secretId);
        var update = Builders<Environment>.Update.Set(x => x.Secrets.FirstMatchingElement().Name, name);

        var result = await Collection.UpdateOneAsync(filter, update);
        if (result.MatchedCount == 0)
        {
            await EnsureExistsAsync(envId);
        }
    }

    private async Task EnsureExistsAsync(Guid envId)
    {
        if (!await AnyAsync(x => x.Id == envId))
        {
            throw new EntityNotFoundException(nameof(Environment), envId.ToString());
        }
    }
}
