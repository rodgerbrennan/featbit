using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Authorization;
using Api.Controllers;
using Application.Bases;
using Application.Services;
using Application.VaultIntegration;
using Domain.Policies;
using Domain.Resources;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Application.IntegrationTests.Controllers;

/// <summary>
/// Change 6, end to end through the real <see cref="DefaultPermissionChecker"/>: env-scoped calls on a deleted
/// environment answer 404 for token callers with the flag on, and 403 otherwise, as upstream.
/// </summary>
[Trait("Category", "Host")]
[Collection(nameof(TestApp))]
public class DeletedEnvironmentTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid DeletedEnv = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid UnpermittedEnv = Guid.NewGuid();

    private readonly TestApp _app;
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    public DeletedEnvironmentTests(TestApp app)
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

    private static PolicyStatement[] PluginEnvStatements =>
    [
        new PolicyStatement
        {
            Id = Guid.NewGuid().ToString(),
            ResourceType = ResourceTypes.Env,
            Effect = EffectType.Allow,
            Actions =
            [
                Permissions.CreateEnvSecret, Permissions.DeleteEnvSecret, Permissions.CanAccessEnv,
                Permissions.ViewEnvSecret
            ],
            Resources = ["project/web:env/prod"]
        }
    ];

    private WebApplicationFactory<Program> Factory(bool enabled, PolicyStatement[]? tokenStatements = null)
    {
        var factory = _app.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("VaultIntegration:Enabled", enabled ? "true" : "false");
            builder.ConfigureTestServices(services =>
            {
                TestAccessTokens.Replace(services, TestAccessTokens.ServiceToken(tokenStatements ?? PluginEnvStatements));

                // the environment was deleted: no resource name, no owner
                var resources = new Mock<IResourceService>();
                resources.Setup(x => x.GetEnvRnAsync(DeletedEnv)).ReturnsAsync((string?)null);
                resources.Setup(x => x.GetEnvOrganizationIdAsync(DeletedEnv)).ReturnsAsync((Guid?)null);
                // an existing env of the caller's organization that its statements do not cover
                resources.Setup(x => x.GetEnvRnAsync(UnpermittedEnv)).ReturnsAsync("project/other:env/x");
                resources.Setup(x => x.GetEnvOrganizationIdAsync(UnpermittedEnv))
                    .ReturnsAsync(TestWorkspace.OrganizationId);
                resources.Setup(x => x.GetProjectOrganizationIdAsync(Project)).ReturnsAsync(TestWorkspace.OrganizationId);
                services.Replace(ServiceDescriptor.Singleton(resources.Object));

                // the real checker (TestApp grants everything)
                services.Replace(ServiceDescriptor.Scoped<IPermissionChecker, DefaultPermissionChecker>());
                var members = new Mock<IMemberService>();
                members.Setup(x => x.GetPermissionsAsync(TestWorkspace.OrganizationId, TestUser.Id))
                    .ReturnsAsync(PluginEnvStatements);
                services.Replace(ServiceDescriptor.Singleton(members.Object));

                services.Replace(ServiceDescriptor.Singleton(new Mock<ISender>().Object));
            });
        });
        _factories.Add(factory);
        return factory;
    }

    public static TheoryData<string, string> PluginCalls => new()
    {
        { "POST", $"/api/v1/envs/{DeletedEnv}/secrets" },
        { "DELETE", $"/api/v1/envs/{DeletedEnv}/secrets/{Guid.NewGuid()}" },
        { "GET", $"/api/v1/projects/{Project}/envs/{DeletedEnv}" }
    };

    private static HttpRequestMessage Request(string method, string uri) =>
        new(new HttpMethod(method), uri) { Content = JsonContent.Create(new { name = "vault-x", type = "server" }) };

    [Theory]
    [MemberData(nameof(PluginCalls))]
    public async Task FlagOn_TokenCaller_DeletedEnv_Returns404(string method, string uri)
    {
        var client = TestAccessTokens.Authorize(Factory(enabled: true).CreateClient());

        var response = await client.SendAsync(Request(method, uri));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);
        Assert.Contains(ErrorCodes.ResourceNotFound, body!.Errors);
    }

    [Theory]
    [MemberData(nameof(PluginCalls))]
    public async Task FlagOff_TokenCaller_DeletedEnv_Returns403AsUpstream(string method, string uri)
    {
        var client = TestAccessTokens.Authorize(Factory(enabled: false).CreateClient());

        var response = await client.SendAsync(Request(method, uri));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(PluginCalls))]
    public async Task FlagOn_JwtCaller_DeletedEnv_Returns403AsUpstream(string method, string uri)
    {
        var client = await _app.CreateAuthenticatedClientAsync(Factory(enabled: true), includeTenantContext: true);

        var response = await client.SendAsync(Request(method, uri));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FlagOn_TokenWithoutEnvScopedPermission_DeletedEnv_Returns403()
    {
        PolicyStatement[] noEnvPermissions =
        [
            new PolicyStatement
            {
                Id = Guid.NewGuid().ToString(), ResourceType = ResourceTypes.AccessToken, Effect = EffectType.Allow,
                Actions = [Permissions.ListAccessTokens], Resources = ["access-token/*"]
            }
        ];
        var client = TestAccessTokens.Authorize(Factory(enabled: true, noEnvPermissions).CreateClient());

        var response = await client.DeleteAsync($"/api/v1/envs/{DeletedEnv}/secrets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FlagOn_TokenCaller_ExistingEnvWithoutPermission_Still403()
    {
        var client = TestAccessTokens.Authorize(Factory(enabled: true).CreateClient());

        var response = await client.DeleteAsync($"/api/v1/envs/{UnpermittedEnv}/secrets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FlagOn_CapabilitiesReportDeletedEnv404()
    {
        var client = TestAccessTokens.Authorize(Factory(enabled: true).CreateClient());

        var body = await (await client.GetAsync("/api/v1/vault-integration/capabilities"))
            .Content.ReadFromJsonAsync<ApiResponse<VaultCapabilitiesVm>>(JsonOptions);

        Assert.Contains(VaultCapabilities.DeletedEnv404, body!.Data!.Capabilities);
    }
}
