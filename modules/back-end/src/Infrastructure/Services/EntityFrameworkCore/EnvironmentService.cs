using System.Text.Json;
using Application.Bases.Exceptions;
using Dapper;
using Domain.EndUsers;
using Domain.Environments;
using Domain.Organizations;
using Domain.Projects;
using Domain.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Environment = Domain.Environments.Environment;

namespace Infrastructure.Services.EntityFrameworkCore;

public class EnvironmentService(AppDbContext dbContext, ILogger<EnvironmentService> logger)
    : EntityFrameworkCoreService<Environment>(dbContext), IEnvironmentService
{
    public async Task<Organization> GetOrganizationAsync(Guid envId)
    {
        var query = from environment in QueryableOf<Environment>()
            join project in QueryableOf<Project>() on environment.ProjectId equals project.Id
            join organization in QueryableOf<Organization>() on project.OrganizationId equals organization.Id
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

        var projects = QueryableOf<Project>();
        var environments = QueryableOf<Environment>();

        var envIds = scopes.Select(Guid.Parse).ToArray();
        var query =
            from environment in environments
            join project in projects on environment.ProjectId equals project.Id
            where envIds.Contains(environment.Id)
            select $"{environment.Id},{project.Name}/{environment.Name}";

        return await query.ToArrayAsync();
    }

    public async Task<RpSecret[]> GetRpSecretsAsync(Guid[] envIds)
    {
        if (envIds.Length == 0)
        {
            return [];
        }

        var projects = QueryableOf<Project>();
        var environments = QueryableOf<Environment>();

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
        var organizations = QueryableOf<Organization>();
        var projects = QueryableOf<Project>();
        var environments = QueryableOf<Environment>();

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
        var organizations = QueryableOf<Organization>();
        var projects = QueryableOf<Project>();
        var environments = QueryableOf<Environment>();

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
        Set.Add(env);

        // add end-user built-in properties
        var builtInProperties = EndUserConsts.BuiltInUserProperties(env.Id);
        SetOf<EndUserProperty>().AddRange(builtInProperties);

        await SaveChangesAsync();
    }

    public async Task AddManyWithBuiltInPropsAsync(ICollection<Environment> envs)
    {
        Set.AddRange(envs);

        // add end-user built-in properties
        var builtInProperties = envs.SelectMany(x => EndUserConsts.BuiltInUserProperties(x.Id));
        SetOf<EndUserProperty>().AddRange(builtInProperties);

        await SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await DeleteOneAsync(id);

        // delete end users
        await SetOf<EndUser>().Where(x => x.EnvId == id).ExecuteDeleteAsync();

        // delete end user properties
        await SetOf<EndUserProperty>().Where(x => x.EnvId == id).ExecuteDeleteAsync();
    }

    public async Task DeleteManyAsync(ICollection<Guid> ids)
    {
        await Set.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();

        // delete end users
        await SetOf<EndUser>().Where(x => x.EnvId != null && ids.Contains(x.EnvId.Value)).ExecuteDeleteAsync();

        // delete end user properties
        await SetOf<EndUserProperty>().Where(x => ids.Contains(x.EnvId)).ExecuteDeleteAsync();
    }

    public async Task<bool> HasKeyBeenUsedAsync(Guid projectId, string key)
    {
        return await AnyAsync(environment =>
            environment.ProjectId == projectId &&
            string.Equals(environment.Key.ToLower(), key.ToLower())
        );
    }

    public async Task<string?> GetProjectEnvAsync(Guid envId)
    {
        var query =
            from project in QueryableOf<Project>()
            join environment in QueryableOf<Environment>() on project.Id equals environment.ProjectId
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


    /// <summary>
    /// Writes every column except <c>secrets</c>. Secrets change only through the targeted
    /// <see cref="AddSecretAsync"/>, <see cref="RemoveSecretAsync"/> and <see cref="UpdateSecretNameAsync"/>,
    /// so a stale copy of the environment can never overwrite a concurrent secret change.
    /// </summary>
    public override async Task UpdateAsync(Environment env)
    {
        Set.Update(env);
        DbContext.Entry(env).Property(x => x.Secrets).IsModified = false;

        await SaveChangesAsync();
    }

    public async Task AddSecretAsync(Guid envId, Secret secret)
    {
        const string sql = """
            UPDATE environments
            SET secrets = COALESCE(secrets, '[]'::jsonb) || jsonb_build_array(@secret::jsonb)
            WHERE id = @envId
            """;

        var secretJson = JsonSerializer.Serialize(secret, ReusableJsonSerializerOptions.Web);
        var affected = await DbConnection.ExecuteAsync(sql, new { envId, secret = secretJson });
        if (affected == 0)
        {
            throw new EntityNotFoundException(nameof(Environment), envId.ToString());
        }
    }

    public async Task<Secret?> RemoveSecretAsync(Guid envId, string secretId)
    {
        // Lock the row first so the array is rebuilt from its latest committed version.
        const string sql = """
            WITH target AS (
                SELECT id, secrets FROM environments WHERE id = @envId FOR UPDATE
            ), removed AS (
                SELECT t.id, s.elem
                FROM target t, jsonb_array_elements(t.secrets) AS s(elem)
                WHERE s.elem ->> 'id' = @secretId
                LIMIT 1
            )
            UPDATE environments e
            SET secrets = COALESCE((
                SELECT jsonb_agg(x.elem ORDER BY x.ord)
                FROM target t, jsonb_array_elements(t.secrets) WITH ORDINALITY AS x(elem, ord)
                WHERE x.elem ->> 'id' IS DISTINCT FROM @secretId
            ), '[]'::jsonb)
            FROM removed r
            WHERE e.id = r.id
            RETURNING r.elem::text
            """;

        var removed = await DbConnection.QuerySingleOrDefaultAsync<string>(sql, new { envId, secretId });
        if (removed != null)
        {
            return JsonSerializer.Deserialize<Secret>(removed, ReusableJsonSerializerOptions.Web);
        }

        await EnsureExistsAsync(envId);
        return null;
    }

    public async Task UpdateSecretNameAsync(Guid envId, string secretId, string name)
    {
        const string sql = """
            WITH target AS (
                SELECT id, secrets FROM environments WHERE id = @envId FOR UPDATE
            )
            UPDATE environments e
            SET secrets = (
                SELECT jsonb_agg(
                    CASE WHEN x.elem ->> 'id' = @secretId
                        THEN jsonb_set(x.elem, '{name}', to_jsonb(@name::text))
                        ELSE x.elem
                    END ORDER BY x.ord)
                FROM jsonb_array_elements(t.secrets) WITH ORDINALITY AS x(elem, ord)
            )
            FROM target t
            WHERE e.id = t.id
              AND t.secrets @> jsonb_build_array(jsonb_build_object('id', @secretId::text))
            """;

        var affected = await DbConnection.ExecuteAsync(sql, new { envId, secretId, name });
        if (affected == 0)
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
