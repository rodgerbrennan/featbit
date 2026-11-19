using System.Security.Claims;
using Api.Authentication;
using Api.Authorization;
using Application;
using Application.Services;
using Application.VaultIntegration;
using Domain.AccessTokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Api.UnitTests.Authorization;

/// <summary>Change 8: access-token callers are confined to their own organization.</summary>
public class TokenOrganizationScopeTests
{
    private static readonly Guid TokenOrg = Guid.NewGuid();
    private static readonly Guid OtherOrg = Guid.NewGuid();

    private readonly Mock<IResourceService> _resources = new();

    private TokenOrganizationScope Sut(bool enabled = true) =>
        new(_resources.Object, Options.Create(new VaultIntegrationOptions { Enabled = enabled }));

    private static HttpContext TokenContext()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], Schemes.OpenApi))
        };
        context.Items[ApplicationConsts.AccessTokenItem] =
            new AccessToken(TokenOrg, Guid.NewGuid(), "t", AccessTokenTypes.Service, []);
        return context;
    }

    private static HttpContext JwtContext() => new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([], Schemes.JwtBearer))
    };

    [Fact]
    public async Task TokenCaller_EnvInOtherOrganization_IsForeignAndMarked()
    {
        var envId = Guid.NewGuid();
        _resources.Setup(x => x.GetEnvOrganizationIdAsync(envId)).ReturnsAsync(OtherOrg);
        var context = TokenContext();

        Assert.True(await Sut().IsForeignEnvAsync(context, envId));
        Assert.True(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task TokenCaller_EnvInOwnOrganization_IsNotForeign()
    {
        var envId = Guid.NewGuid();
        _resources.Setup(x => x.GetEnvOrganizationIdAsync(envId)).ReturnsAsync(TokenOrg);
        var context = TokenContext();

        Assert.False(await Sut().IsForeignEnvAsync(context, envId));
        Assert.False(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task TokenCaller_UnknownEnv_IsNotForeign()
    {
        // a missing environment keeps upstream behavior here (change 6 handles deleted environments)
        var envId = Guid.NewGuid();
        _resources.Setup(x => x.GetEnvOrganizationIdAsync(envId)).ReturnsAsync((Guid?)null);
        var context = TokenContext();

        Assert.False(await Sut().IsForeignEnvAsync(context, envId));
        Assert.False(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task TokenCaller_ProjectInOtherOrganization_IsForeignAndMarked()
    {
        var projectId = Guid.NewGuid();
        _resources.Setup(x => x.GetProjectOrganizationIdAsync(projectId)).ReturnsAsync(OtherOrg);
        var context = TokenContext();

        Assert.True(await Sut().IsForeignProjectAsync(context, projectId));
        Assert.True(ResourceNotFoundMarker.IsMarked(context));
    }

    [Fact]
    public async Task TokenCaller_ProjectInOwnOrganization_IsNotForeign()
    {
        var projectId = Guid.NewGuid();
        _resources.Setup(x => x.GetProjectOrganizationIdAsync(projectId)).ReturnsAsync(TokenOrg);

        Assert.False(await Sut().IsForeignProjectAsync(TokenContext(), projectId));
    }

    [Fact]
    public async Task FlagOff_TokenCaller_IsNeverForeignAndNeverLooksUp()
    {
        var envId = Guid.NewGuid();
        _resources.Setup(x => x.GetEnvOrganizationIdAsync(envId)).ReturnsAsync(OtherOrg);
        var context = TokenContext();

        Assert.False(await Sut(enabled: false).IsForeignEnvAsync(context, envId));
        Assert.False(await Sut(enabled: false).IsForeignProjectAsync(context, Guid.NewGuid()));
        Assert.False(ResourceNotFoundMarker.IsMarked(context));
        _resources.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task JwtCaller_IsNeverForeignAndNeverLooksUp()
    {
        var envId = Guid.NewGuid();
        _resources.Setup(x => x.GetEnvOrganizationIdAsync(envId)).ReturnsAsync(OtherOrg);

        Assert.False(await Sut().IsForeignEnvAsync(JwtContext(), envId));
        _resources.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TokenCaller_WithoutAccessTokenItem_IsTreatedAsForeign()
    {
        // fail closed: an OpenApi identity whose organization cannot be read must not reach any environment
        var envId = Guid.NewGuid();
        _resources.Setup(x => x.GetEnvOrganizationIdAsync(envId)).ReturnsAsync(TokenOrg);
        var context = TokenContext();
        context.Items.Remove(ApplicationConsts.AccessTokenItem);

        Assert.True(await Sut().IsForeignEnvAsync(context, envId));
    }

    [Fact]
    public async Task SameEnvTwice_LooksUpOnce()
    {
        var envId = Guid.NewGuid();
        _resources.Setup(x => x.GetEnvOrganizationIdAsync(envId)).ReturnsAsync(TokenOrg);
        var sut = Sut();
        var context = TokenContext();

        await sut.IsForeignEnvAsync(context, envId);
        await sut.IsForeignEnvAsync(context, envId);

        _resources.Verify(x => x.GetEnvOrganizationIdAsync(envId), Times.Once);
    }
}
