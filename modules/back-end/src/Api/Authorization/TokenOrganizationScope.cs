using Api.Authentication;
using Application;
using Application.Services;
using Application.VaultIntegration;
using Domain.AccessTokens;
using Microsoft.Extensions.Options;

namespace Api.Authorization;

/// <summary>Scoped per request: each environment or project is looked up once.</summary>
public class TokenOrganizationScope(IResourceService resourceService, IOptions<VaultIntegrationOptions> options)
    : ITokenOrganizationScope
{
    private readonly Dictionary<(char Kind, Guid Id), bool> _foreign = new();

    public Task<bool> IsForeignEnvAsync(HttpContext context, Guid envId) =>
        IsForeignAsync(context, ('e', envId), () => resourceService.GetEnvOrganizationIdAsync(envId));

    public Task<bool> IsForeignProjectAsync(HttpContext context, Guid projectId) =>
        IsForeignAsync(context, ('p', projectId), () => resourceService.GetProjectOrganizationIdAsync(projectId));

    private async Task<bool> IsForeignAsync(HttpContext context, (char, Guid) target, Func<Task<Guid?>> ownerOf)
    {
        // JWT callers and a disabled integration keep upstream behavior
        if (!options.Value.Enabled || context.User.Identity?.AuthenticationType != Schemes.OpenApi)
        {
            return false;
        }

        if (!_foreign.TryGetValue(target, out var foreign))
        {
            // fail closed: a token whose organization cannot be read reaches no environment or project
            var tokenOrganizationId = context.Items[ApplicationConsts.AccessTokenItem] is AccessToken token
                ? token.OrganizationId
                : (Guid?)null;

            // a target that does not exist is not foreign: callers keep their upstream answer for it
            var ownerId = await ownerOf();
            foreign = ownerId.HasValue && ownerId != tokenOrganizationId;
            _foreign[target] = foreign;
        }

        if (foreign)
        {
            ResourceNotFoundMarker.Mark(context);
        }

        return foreign;
    }
}
