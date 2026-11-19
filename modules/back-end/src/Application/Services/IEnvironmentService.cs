using Domain.Environments;
using Environment = Domain.Environments.Environment;

namespace Application.Services;

public interface IEnvironmentService : IService<Environment>
{
    Task<Domain.Organizations.Organization> GetOrganizationAsync(Guid envId);

    Task<string[]> GetServesAsync(string[] scopes);

    Task<RpSecret[]> GetRpSecretsAsync(Guid[] envIds);

    Task<ResourceDescriptor> GetResourceDescriptorAsync(Guid envId);

    Task<ICollection<SecretCache>> GetSecretCachesAsync();

    Task AddWithBuiltInPropsAsync(Environment env);

    Task AddManyWithBuiltInPropsAsync(ICollection<Environment> envs);

    Task DeleteAsync(Guid id);

    Task DeleteManyAsync(ICollection<Guid> ids);

    Task<bool> HasKeyBeenUsedAsync(Guid projectId, string key);

    Task<string> GetProjectEnvAsync(Guid envId);

    Task AddSecretAsync(Guid envId, Secret secret);

    /// <returns>The removed secret, or <c>null</c> when the environment holds no secret with that ID.</returns>
    Task<Secret> RemoveSecretAsync(Guid envId, string secretId);

    Task UpdateSecretNameAsync(Guid envId, string secretId, string name);
}
