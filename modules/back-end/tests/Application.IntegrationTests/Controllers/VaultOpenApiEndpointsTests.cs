using System.Net;
using System.Net.Http.Json;
using Application.Services;
using Application.VaultIntegration;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Application.IntegrationTests.Controllers;

/// <summary>
/// Change 1: the secret and access-token actions the vault plugin calls accept access-token callers, only with
/// <c>VaultIntegration:Enabled</c> on.
/// </summary>
[Trait("Category", "Host")]
[Collection(nameof(TestApp))]
public class VaultOpenApiEndpointsTests : IDisposable
{
    private static readonly Guid EnvId = Guid.NewGuid();

    private readonly TestApp _app;
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    public VaultOpenApiEndpointsTests(TestApp app)
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
        {
            builder.UseSetting("VaultIntegration:Enabled", enabled ? "true" : "false");
            builder.ConfigureTestServices(services =>
            {
                TestAccessTokens.Replace(services);

                // the env belongs to the token's organization (change 8 looks it up)
                var resources = new Mock<IResourceService>();
                resources.Setup(x => x.GetEnvOrganizationIdAsync(EnvId)).ReturnsAsync(TestWorkspace.OrganizationId);
                resources.Setup(x => x.GetEnvRnAsync(EnvId)).ReturnsAsync("project/web:env/prod");
                services.Replace(ServiceDescriptor.Singleton(resources.Object));

                services.Replace(ServiceDescriptor.Singleton(new Mock<ISender>().Object));
            });
        });
        _factories.Add(factory);
        return factory;
    }

    private HttpClient TokenClient(bool enabled) => TestAccessTokens.Authorize(Factory(enabled).CreateClient());

    private static HttpRequestMessage Request(string method, string uri) =>
        new(new HttpMethod(method), uri) { Content = JsonContent.Create(new { }) };

    public static TheoryData<string, string> MarkedActions => new()
    {
        { "POST", $"/api/v1/envs/{EnvId}/secrets" },
        { "DELETE", $"/api/v1/envs/{EnvId}/secrets/{Guid.NewGuid()}" },
        { "GET", "/api/v1/access-tokens?pageIndex=0&pageSize=1" },
        { "POST", "/api/v1/access-tokens" },
        { "DELETE", $"/api/v1/access-tokens/{Guid.NewGuid()}" }
    };

    public static TheoryData<string, string> UnmarkedActions => new()
    {
        { "PUT", $"/api/v1/access-tokens/{Guid.NewGuid()}/toggle" },
        { "PUT", $"/api/v1/access-tokens/{Guid.NewGuid()}" },
        { "PUT", $"/api/v1/envs/{EnvId}/secrets/{Guid.NewGuid()}" }
    };

    [Theory]
    [MemberData(nameof(MarkedActions))]
    public async Task FlagOn_TokenCaller_MarkedAction_Returns200(string method, string uri)
    {
        var response = await TokenClient(enabled: true).SendAsync(Request(method, uri));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(MarkedActions))]
    public async Task FlagOff_TokenCaller_MarkedAction_Returns403AsUpstream(string method, string uri)
    {
        var response = await TokenClient(enabled: false).SendAsync(Request(method, uri));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(UnmarkedActions))]
    public async Task FlagOn_TokenCaller_UnmarkedAction_Returns403(string method, string uri)
    {
        var response = await TokenClient(enabled: true).SendAsync(Request(method, uri));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JwtCaller_MarkedAction_IsUnchanged(bool enabled)
    {
        var client = await _app.CreateAuthenticatedClientAsync(Factory(enabled), includeTenantContext: true);

        var response = await client.SendAsync(Request("POST", $"/api/v1/envs/{EnvId}/secrets"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FlagOff_MarkedActions_AreNotInOpenApiDocument()
    {
        var document = await Factory(enabled: false).CreateClient().GetStringAsync("/openapi/OpenApi.json");

        Assert.DoesNotContain("/api/v1/envs/{envId}/secrets", document);
        Assert.DoesNotContain("/api/v1/access-tokens", document);
    }

    [Fact]
    public async Task FlagOn_MarkedActions_AreInOpenApiDocument()
    {
        var document = await Factory(enabled: true).CreateClient().GetStringAsync("/openapi/OpenApi.json");

        Assert.Contains("/api/v1/envs/{envId}/secrets", document);
        Assert.Contains("/api/v1/envs/{envId}/secrets/{id}", document);
        Assert.Contains("/api/v1/access-tokens", document);
        Assert.Contains("/api/v1/access-tokens/{id}", document);
        Assert.DoesNotContain("/api/v1/access-tokens/{id}/toggle", document);
    }

    [Fact]
    public async Task FlagOn_CapabilitiesReportOpenApiSecrets()
    {
        var response = await TokenClient(enabled: true).GetAsync("/api/v1/vault-integration/capabilities");

        var body = await response.Content.ReadFromJsonAsync<Api.Controllers.ApiResponse<VaultCapabilitiesVm>>(
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains(VaultCapabilities.OpenApiSecrets, body!.Data!.Capabilities);
    }
}
