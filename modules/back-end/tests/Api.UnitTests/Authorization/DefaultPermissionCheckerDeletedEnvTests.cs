using System.Security.Claims;
using Api.Authentication;
using Api.Authorization;
using Application.Services;
using Application.VaultIntegration;
using Domain.Policies;
using Domain.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.UnitTests.Authorization;

/// <summary>
/// Change 6: an env-scoped call on an environment that no longer exists is marked "not found" (404) instead of a
/// plain denial (403), for token callers that hold some env-scoped permission, with the flag on.
/// </summary>
public class DefaultPermissionCheckerDeletedEnvTests
{
    private static readonly Guid DeletedEnv = Guid.NewGuid();
    private static readonly Guid ExistingEnv = Guid.NewGuid();

    private readonly Mock<IResourceService> _resources = new();
    private readonly Mock<IRequestPermissions> _permissions = new();

    public DefaultPermissionCheckerDeletedEnvTests()
    {
        _resources.Setup(x => x.GetEnvRnAsync(DeletedEnv)).ReturnsAsync((string?)null);
        _resources.Setup(x => x.GetEnvRnAsync(ExistingEnv)).ReturnsAsync("project/web:env/prod");
    }

    private DefaultPermissionChecker Sut(bool enabled = true) => new(
        _resources.Object, _permissions.Object, Mock.Of<ITokenOrganizationScope>(),
        Options.Create(new VaultIntegrationOptions { Enabled = enabled }),
        NullLogger<DefaultPermissionChecker>.Instance);

    private void Holds(params string[] actions) =>
        _permissions.Setup(x => x.GetAsync(It.IsAny<HttpContext>())).ReturnsAsync(
        [
            new PolicyStatement
            {
                Id = Guid.NewGuid().ToString(),
                ResourceType = ResourceTypes.Env,
                Effect = EffectType.Allow,
                Actions = actions,
                Resources = ["project/web:env/prod"]
            }
        ]);

    private static HttpContext Context(Guid envId, string scheme = Schemes.OpenApi)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], scheme))
        };
        context.Request.RouteValues["envId"] = envId.ToString();
        return context;
    }

    public static TheoryData<string> EnvScopedPermissions =>
    [
        Permissions.CanAccessEnv,
        Permissions.CreateEnvSecret,
        Permissions.DeleteEnvSecret,
        Permissions.ViewEnvSecret
    ];

    [Theory]
    [MemberData(nameof(EnvScopedPermissions))]
    public async Task DeletedEnv_TokenWithEnvScopedPermission_DeniedAndMarkedNotFound(string permission)
    {
        Holds(Permissions.CanAccessEnv, Permissions.CreateEnvSecret, Permissions.DeleteEnvSecret);
        var context = Context(DeletedEnv);

        Assert.False(await Sut().IsGrantedAsync(context, new PermissionRequirement(permission)));
        Assert.True(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task DeletedEnv_TokenHoldingADifferentEnvScopedPermission_StillMarked()
    {
        // the caller holds env-scoped permissions somewhere, just not the one asked for
        Holds(Permissions.CanAccessEnv);
        var context = Context(DeletedEnv);

        Assert.False(await Sut().IsGrantedAsync(context, new PermissionRequirement(Permissions.DeleteEnvSecret)));
        Assert.True(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task DeletedEnv_TokenWithWildcardStatement_Marked()
    {
        _permissions.Setup(x => x.GetAsync(It.IsAny<HttpContext>())).ReturnsAsync(
        [
            new PolicyStatement
            {
                Id = Guid.NewGuid().ToString(), ResourceType = "*", Effect = EffectType.Allow,
                Actions = ["*"], Resources = ["*"]
            }
        ]);
        var context = Context(DeletedEnv);

        Assert.False(await Sut().IsGrantedAsync(context, new PermissionRequirement(Permissions.CanAccessEnv)));
        Assert.True(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task DeletedEnv_TokenWithoutEnvScopedPermission_NotMarked()
    {
        // leak check: a caller with no env-scoped permission learns nothing about which env IDs exist
        _permissions.Setup(x => x.GetAsync(It.IsAny<HttpContext>())).ReturnsAsync(
        [
            new PolicyStatement
            {
                Id = Guid.NewGuid().ToString(), ResourceType = ResourceTypes.AccessToken, Effect = EffectType.Allow,
                Actions = [Permissions.ListAccessTokens], Resources = ["access-token/*"]
            }
        ]);
        var context = Context(DeletedEnv);

        Assert.False(await Sut().IsGrantedAsync(context, new PermissionRequirement(Permissions.CanAccessEnv)));
        Assert.False(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task DeletedEnv_OnlyDenyStatements_NotMarked()
    {
        _permissions.Setup(x => x.GetAsync(It.IsAny<HttpContext>())).ReturnsAsync(
        [
            new PolicyStatement
            {
                Id = Guid.NewGuid().ToString(), ResourceType = ResourceTypes.Env, Effect = EffectType.Deny,
                Actions = [Permissions.CanAccessEnv], Resources = ["project/*:env/*"]
            }
        ]);
        var context = Context(DeletedEnv);

        Assert.False(await Sut().IsGrantedAsync(context, new PermissionRequirement(Permissions.CanAccessEnv)));
        Assert.False(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task DeletedEnv_OtherEnvPermission_NotMarked()
    {
        // only the four env-scoped permissions the plugin uses answer 404
        Holds(Permissions.CanAccessEnv, Permissions.UpdateEnvSettings);
        var context = Context(DeletedEnv);

        Assert.False(await Sut().IsGrantedAsync(context, new PermissionRequirement(Permissions.UpdateEnvSettings)));
        Assert.False(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task DeletedEnv_FlagOff_NotMarked()
    {
        Holds(Permissions.CanAccessEnv);
        var context = Context(DeletedEnv);

        Assert.False(await Sut(enabled: false).IsGrantedAsync(context, new PermissionRequirement(Permissions.CanAccessEnv)));
        Assert.False(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task DeletedEnv_JwtCaller_NotMarked()
    {
        Holds(Permissions.CanAccessEnv);
        var context = Context(DeletedEnv, Schemes.JwtBearer);

        Assert.False(await Sut().IsGrantedAsync(context, new PermissionRequirement(Permissions.CanAccessEnv)));
        Assert.False(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task ExistingEnv_WithoutPermission_DeniedNotMarked()
    {
        Holds(Permissions.CanAccessEnv);
        var context = Context(ExistingEnv);

        Assert.False(await Sut().IsGrantedAsync(context, new PermissionRequirement(Permissions.DeleteEnvSecret)));
        Assert.False(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task ExistingEnv_WithPermission_Granted()
    {
        Holds(Permissions.DeleteEnvSecret);
        var context = Context(ExistingEnv);

        Assert.True(await Sut().IsGrantedAsync(context, new PermissionRequirement(Permissions.DeleteEnvSecret)));
        Assert.False(ResourceNotFoundMarker.IsMarked(context));
    }
}
