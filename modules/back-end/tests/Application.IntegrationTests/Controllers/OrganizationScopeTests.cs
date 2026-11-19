using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Authorization;
using Api.Controllers;
using Application.Bases;
using Application.Identity;
using Application.Services;
using Application.Users;
using Domain.Policies;
using Domain.Resources;
using Domain.Users;
using Infrastructure.Caches;
using Infrastructure.MQ;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.IntegrationTests.Controllers;

/// <summary>
/// Change 8, end to end through the real authentication, <see cref="DefaultPermissionChecker"/> and
/// result handler. Two organizations hold a project <c>web</c> with an environment <c>prod</c>; the caller's
/// statements name <c>project/web:env/prod</c>, which matches both by key.
/// </summary>
[Trait("Category", "Host")]
[Collection(nameof(TestApp))]
public class OrganizationScopeTests : IClassFixture<OrganizationScopeTests.App>, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // organization A is the caller's (TestWorkspace.OrganizationId); B is another organization
    private static readonly Guid OtherOrg = Guid.NewGuid();
    private static readonly Guid ProjectA = Guid.NewGuid();
    private static readonly Guid ProjectB = Guid.NewGuid();
    private static readonly Guid EnvA = Guid.NewGuid();
    private static readonly Guid EnvB = Guid.NewGuid();

    private readonly App _app;
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    public OrganizationScopeTests(App app)
    {
        _app = app;
    }

    public void Dispose()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }
    }

    private WebApplicationFactory<Program> Factory(bool enabled)
    {
        var factory = _app.WithWebHostBuilder(builder =>
            builder.UseSetting("VaultIntegration:Enabled", enabled ? "true" : "false"));
        _factories.Add(factory);
        return factory;
    }

    private HttpClient TokenClient(bool enabled = true) => TestAccessTokens.Authorize(Factory(enabled).CreateClient());

    private async Task<HttpClient> JwtClient(bool enabled = true)
    {
        var factory = Factory(enabled);
        using var scope = factory.Services.CreateScope();
        var tokens = await scope.ServiceProvider.GetRequiredService<IIdentityService>()
            .IssueTokensAsync(TestUser.Instance(), "::1");

        var client = factory.CreateClient();
        TestTenantContext.AddHeaders(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, tokens.AccessToken);
        return client;
    }

    public static TheoryData<string, string> ForeignEnvRequests => new()
    {
        // attribute-checked env, flag and segment permissions
        { "GET", $"/api/v1/envs/{EnvB}/feature-flags" },
        { "GET", $"/api/v1/envs/{EnvB}/feature-flags/some-flag" },
        { "GET", $"/api/v1/envs/{EnvB}/segments" },
        { "GET", $"/api/v1/envs/{EnvB}/segments/{Guid.NewGuid()}" },
        { "GET", $"/api/v1/projects/{ProjectA}/envs/{EnvB}" },
        { "POST", $"/api/v1/envs/{EnvB}/secrets" },
        { "DELETE", $"/api/v1/envs/{EnvB}/secrets/{Guid.NewGuid()}" },
        // env resolved from {id}, under the caller's own project
        { "PUT", $"/api/v1/projects/{ProjectA}/envs/{EnvB}" },
        { "DELETE", $"/api/v1/projects/{ProjectA}/envs/{EnvB}" },
        // env-routed endpoints with no permission attribute
        { "GET", $"/api/v1/envs/{EnvB}/experiments" },
        { "PUT", $"/api/v1/envs/{EnvB}/feature-flags/some-flag/targeting" },
        // a foreign target env on an own source env (not [OpenApi] today; must not confirm the target either)
        { "GET", $"/api/v1/envs/{EnvA}/feature-flags/some-flag/compare-with/{EnvB}" },
        // project-routed, including the unattributed project GET that returns environments and their secrets
        { "GET", $"/api/v1/projects/{ProjectB}" },
        { "PUT", $"/api/v1/projects/{ProjectB}" },
        { "POST", $"/api/v1/projects/{ProjectB}/envs" }
    };

    private static HttpRequestMessage Request(string method, string uri) =>
        new(new HttpMethod(method), uri) { Content = JsonContent.Create(new { }) };

    [Theory]
    [MemberData(nameof(ForeignEnvRequests))]
    public async Task FlagOn_TokenCaller_OtherOrganization_Returns404(string method, string uri)
    {
        var response = await TokenClient().SendAsync(Request(method, uri));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);
        Assert.Contains(ErrorCodes.ResourceNotFound, body!.Errors);
    }

    [Theory]
    [InlineData("/api/v1/envs/{env}/feature-flags")]
    [InlineData("/api/v1/envs/{env}/segments")]
    [InlineData("/api/v1/projects/{project}/envs/{env}")]
    [InlineData("/api/v1/envs/{env}/experiments")]
    [InlineData("/api/v1/projects/{project}")]
    public async Task FlagOn_TokenCaller_OwnOrganization_Returns200(string template)
    {
        var uri = template.Replace("{env}", EnvA.ToString()).Replace("{project}", ProjectA.ToString());

        var response = await TokenClient().GetAsync(uri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FlagOff_TokenCaller_OtherOrganization_KeepsUpstreamBehavior()
    {
        // upstream: key-based resource names match the other organization's environment
        var response = await TokenClient(enabled: false).GetAsync($"/api/v1/envs/{EnvB}/feature-flags");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FlagOff_TokenCaller_NoPermissionEndpoint_KeepsUpstreamBehavior()
    {
        var response = await TokenClient(enabled: false).GetAsync($"/api/v1/envs/{EnvB}/experiments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FlagOn_JwtCaller_IsUnchanged()
    {
        var client = await JwtClient();
        _app.Resources.Invocations.Clear(); // the mock is shared by the class; tests in a class run one at a time

        var response = await client.GetAsync($"/api/v1/envs/{EnvB}/feature-flags");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _app.Resources.Verify(x => x.GetEnvOrganizationIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task FlagOn_TokenCaller_DeniedOnOwnEnv_Still403()
    {
        // a foreign-organization 404 must not change ordinary denials
        var response = await TokenClient().DeleteAsync($"/api/v1/envs/{EnvA}/feature-flags/some-flag");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Real permission checker; storage and the mediator are mocked.</summary>
    public sealed class App : WebApplicationFactory<Program>
    {
        public Mock<IResourceService> Resources { get; } = new();

        private static PolicyStatement[] SameKeyStatements =>
        [
            new PolicyStatement
            {
                Id = Guid.NewGuid().ToString(),
                ResourceType = "env",
                Effect = EffectType.Allow,
                Actions = ["CanAccessEnv", "CreateEnvSecret", "DeleteEnvSecret", "UpdateEnvSettings", "DeleteEnv"],
                Resources = ["project/web:env/prod"]
            },
            new PolicyStatement
            {
                Id = Guid.NewGuid().ToString(),
                ResourceType = "project",
                Effect = EffectType.Allow,
                Actions = ["UpdateProjectSettings", "CreateEnv"],
                Resources = ["project/web", "project/web:env/*"]
            }
        ];

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(MqProvider.SectionName, MqProvider.None);
            builder.UseSetting(CacheProvider.SectionName, CacheProvider.None);

            Resources.Setup(x => x.GetEnvOrganizationIdAsync(EnvA)).ReturnsAsync(TestWorkspace.OrganizationId);
            Resources.Setup(x => x.GetEnvOrganizationIdAsync(EnvB)).ReturnsAsync(OtherOrg);
            Resources.Setup(x => x.GetProjectOrganizationIdAsync(ProjectA)).ReturnsAsync(TestWorkspace.OrganizationId);
            Resources.Setup(x => x.GetProjectOrganizationIdAsync(ProjectB)).ReturnsAsync(OtherOrg);
            Resources.Setup(x => x.GetEnvRnAsync(It.IsAny<Guid>())).ReturnsAsync("project/web:env/prod");
            Resources.Setup(x => x.GetProjectRnAsync(It.IsAny<Guid>())).ReturnsAsync("project/web");
            Resources.Setup(x => x.GetFlagRnAsync(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync("project/web:env/prod:flag/some-flag");
            Resources.Setup(x => x.GetSegmentRnAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync("project/web:env/prod:segment/s");

            builder.ConfigureServices(collection =>
            {
                collection.Replace(ServiceDescriptor.Scoped<IPasswordHasher<User>, TestPasswordHasher>());
                collection.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new TestCurrentUser(TestUser.Id)));
                collection.Replace(ServiceDescriptor.Transient<IWorkspaceService, TestWorkspaceService>());
                collection.Replace(ServiceDescriptor.Transient<IUserService, TestUserService>());
                collection.Replace(ServiceDescriptor.Singleton(TestTenantContext.CreateOrganizationService().Object));
                collection.Replace(ServiceDescriptor.Transient<IRefreshTokenService, TestRefreshTokenService>());

                var members = new Mock<IMemberService>();
                members.Setup(x => x.GetPermissionsAsync(TestWorkspace.OrganizationId, TestUser.Id))
                    .ReturnsAsync(SameKeyStatements);
                collection.Replace(ServiceDescriptor.Singleton(members.Object));
                collection.Replace(ServiceDescriptor.Singleton(Resources.Object));
                collection.Replace(ServiceDescriptor.Singleton(new Mock<ISender>().Object));
                TestAccessTokens.Replace(collection, TestAccessTokens.ServiceToken(SameKeyStatements));

                var hostedServices = collection.Where(x =>
                    x.ServiceType.IsAssignableTo(typeof(IHostedService)) &&
                    x.ImplementationType?.FullName?.Contains("Microsoft") == false
                ).ToArray();
                foreach (var service in hostedServices)
                {
                    collection.Remove(service);
                }
            });

            builder.ConfigureLogging(logging =>
                logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None));
        }
    }
}
