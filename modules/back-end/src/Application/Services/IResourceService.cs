#nullable enable

using Application.Resources;
using Domain.Resources;

namespace Application.Services;

public interface IResourceService
{
    Task<IEnumerable<Resource>> GetResourcesAsync(Guid organizationId, ResourceFilter filter);

    Task<string?> GetProjectRnAsync(Guid projectId);

    Task<string?> GetEnvRnAsync(Guid envId);

    Task<string?> GetFlagRnAsync(Guid envId, string key);

    Task<string?> GetSegmentRnAsync(Guid envId, Guid id);

    /// <summary>The ID of the organization that owns the environment, or <c>null</c> if it does not exist.</summary>
    Task<Guid?> GetEnvOrganizationIdAsync(Guid envId);

    /// <summary>The ID of the organization that owns the project, or <c>null</c> if it does not exist.</summary>
    Task<Guid?> GetProjectOrganizationIdAsync(Guid projectId);
}