namespace Api.Authorization;

/// <summary>
/// Vault integration change 8: confines access-token callers to environments and projects of their own
/// organization. Resource names are built from keys, so without this a token's statements also match
/// same-keyed resources in other organizations.
/// </summary>
public interface ITokenOrganizationScope
{
    /// <summary>
    /// True when the caller is an access token, <c>VaultIntegration:Enabled</c> is on, and the environment exists
    /// in another organization. Also marks the request with <see cref="ResourceNotFoundMarker"/>, so the denial
    /// answers 404 and does not confirm the environment to the caller.
    /// </summary>
    Task<bool> IsForeignEnvAsync(HttpContext context, Guid envId);

    /// <inheritdoc cref="IsForeignEnvAsync"/>
    Task<bool> IsForeignProjectAsync(HttpContext context, Guid projectId);
}
