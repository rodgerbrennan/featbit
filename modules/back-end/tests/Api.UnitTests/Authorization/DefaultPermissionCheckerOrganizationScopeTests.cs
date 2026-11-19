using Application.VaultIntegration;
using Microsoft.Extensions.Options;
using Api.Authorization;
using Application.Services;
using Domain.Policies;
using Domain.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.UnitTests.Authorization;

/// <summary>
/// Change 8: every environment or project ID the permission checker resolves goes through
/// <see cref="ITokenOrganizationScope"/>; a foreign one is denied even when the caller's statements match its
/// key-based resource name.
/// </summary>
public class DefaultPermissionCheckerOrganizationScopeTests
{
    private const string EnvRn = "project/web:env/prod";

    private readonly Mock<IResourceService> _resources = new();
    private readonly Mock<IRequestPermissions> _permissions = new();
    private readonly Mock<ITokenOrganizationScope> _scope = new();

    private DefaultPermissionChecker Sut() => new(
        _resources.Object, _permissions.Object, _scope.Object, Options.Create(new VaultIntegrationOptions()),
        NullLogger<DefaultPermissionChecker>.Instance);

    private void AllowEverythingOnSameKeys()
    {
        // statements that match the same-keyed resources of any organization
        _permissions.Setup(x => x.GetAsync(It.IsAny<HttpContext>())).ReturnsAsync(
        [
            new PolicyStatement
            {
                Id = Guid.NewGuid().ToString(),
                ResourceType = "*",
                Effect = EffectType.Allow,
                Actions = ["*"],
                Resources = ["*"]
            }
        ]);
        _resources.Setup(x => x.GetEnvRnAsync(It.IsAny<Guid>())).ReturnsAsync(EnvRn);
        _resources.Setup(x => x.GetProjectRnAsync(It.IsAny<Guid>())).ReturnsAsync("project/web");
        _resources.Setup(x => x.GetFlagRnAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync($"{EnvRn}:flag/f");
        _resources.Setup(x => x.GetSegmentRnAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync($"{EnvRn}:segment/s");
    }

    private static HttpContext Route(params (string Key, object Value)[] values)
    {
        var context = new DefaultHttpContext();
        foreach (var (key, value) in values)
        {
            context.Request.RouteValues[key] = value;
        }

        return context;
    }

    public static TheoryData<string, string> EnvScopedRoutes => new()
    {
        { Permissions.CanAccessEnv, "envId" },
        { Permissions.UpdateEnvSettings, "id" },
        { Permissions.CreateEnvSecret, "envId" },
        { Permissions.DeleteEnvSecret, "envId" },
        { Permissions.ToggleFlag, "envId+key" },
        { Permissions.CreateFlag, "envId" },
        { Permissions.UpdateSegmentName, "envId+segmentId" },
        { Permissions.CreateSegment, "envId" }
    };

    private static HttpContext EnvRoute(string shape, Guid envId) => shape switch
    {
        "envId" => Route(("envId", envId.ToString())),
        "id" => Route(("projectId", Guid.NewGuid().ToString()), ("id", envId.ToString())),
        "envId+key" => Route(("envId", envId.ToString()), ("key", "f")),
        "envId+segmentId" => Route(("envId", envId.ToString()), ("segmentId", Guid.NewGuid().ToString())),
        _ => throw new ArgumentOutOfRangeException(nameof(shape))
    };

    [Theory]
    [MemberData(nameof(EnvScopedRoutes))]
    public async Task ForeignEnv_IsDeniedDespiteMatchingStatements(string permission, string shape)
    {
        AllowEverythingOnSameKeys();
        var envId = Guid.NewGuid();
        _scope.Setup(x => x.IsForeignEnvAsync(It.IsAny<HttpContext>(), envId)).ReturnsAsync(true);

        Assert.False(await Sut().IsGrantedAsync(EnvRoute(shape, envId), new PermissionRequirement(permission)));
    }

    [Theory]
    [MemberData(nameof(EnvScopedRoutes))]
    public async Task OwnEnv_IsGranted(string permission, string shape)
    {
        AllowEverythingOnSameKeys();
        var envId = Guid.NewGuid();
        _scope.Setup(x => x.IsForeignEnvAsync(It.IsAny<HttpContext>(), envId)).ReturnsAsync(false);

        Assert.True(await Sut().IsGrantedAsync(EnvRoute(shape, envId), new PermissionRequirement(permission)));
        _scope.Verify(x => x.IsForeignEnvAsync(It.IsAny<HttpContext>(), envId), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("projectId")]
    [InlineData("id")]
    public async Task ForeignProject_IsDeniedDespiteMatchingStatements(string routeKey)
    {
        AllowEverythingOnSameKeys();
        var projectId = Guid.NewGuid();
        _scope.Setup(x => x.IsForeignProjectAsync(It.IsAny<HttpContext>(), projectId)).ReturnsAsync(true);

        var granted = await Sut().IsGrantedAsync(
            Route((routeKey, projectId.ToString())), new PermissionRequirement(Permissions.UpdateProjectSettings));

        Assert.False(granted);
    }

    [Fact]
    public async Task CreateEnvInForeignProject_IsDenied()
    {
        AllowEverythingOnSameKeys();
        var projectId = Guid.NewGuid();
        _scope.Setup(x => x.IsForeignProjectAsync(It.IsAny<HttpContext>(), projectId)).ReturnsAsync(true);

        var granted = await Sut().IsGrantedAsync(
            Route(("projectId", projectId.ToString())), new PermissionRequirement(Permissions.CreateEnv));

        Assert.False(granted);
    }

    [Fact]
    public async Task OwnProject_IsGranted()
    {
        AllowEverythingOnSameKeys();
        var projectId = Guid.NewGuid();
        _scope.Setup(x => x.IsForeignProjectAsync(It.IsAny<HttpContext>(), projectId)).ReturnsAsync(false);

        var granted = await Sut().IsGrantedAsync(
            Route(("id", projectId.ToString())), new PermissionRequirement(Permissions.UpdateProjectSettings));

        Assert.True(granted);
    }
}
