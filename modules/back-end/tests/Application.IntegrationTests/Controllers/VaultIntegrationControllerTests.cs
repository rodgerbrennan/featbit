using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Controllers;
using Application.VaultIntegration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Application.IntegrationTests.Controllers;

/// <summary>Change 7: the <c>VaultIntegration</c> flag and the capabilities endpoint.</summary>
[Trait("Category", "Host")]
[Collection(nameof(TestApp))]
public class VaultIntegrationControllerTests : IDisposable
{
    private const string Uri = "/api/v1/vault-integration/capabilities";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TestApp _app;
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    public VaultIntegrationControllerTests(TestApp app)
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

    private WebApplicationFactory<Program> App(params (string Key, string Value)[] settings)
    {
        var factory = _app.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services => TestAccessTokens.Replace(services));
        });
        _factories.Add(factory);
        return factory;
    }

    private WebApplicationFactory<Program> Enabled(params (string Key, string Value)[] extra) =>
        App([("VaultIntegration:Enabled", "true"), .. extra]);

    private static HttpClient TokenClient(WebApplicationFactory<Program> factory) =>
        TestAccessTokens.Authorize(factory.CreateClient());

    [Fact]
    public async Task FlagOff_JwtCaller_Returns404()
    {
        var client = await _app.CreateAuthenticatedClientAsync(App(), includeTenantContext: true);

        var response = await client.GetAsync(Uri);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FlagOff_TokenCaller_Returns404()
    {
        var response = await TokenClient(App()).GetAsync(Uri);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FlagExplicitlyFalse_Returns404()
    {
        var response = await TokenClient(App(("VaultIntegration:Enabled", "false"))).GetAsync(Uri);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FlagOn_Unauthenticated_Returns401()
    {
        var response = await Enabled().CreateClient().GetAsync(Uri);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenStub_OnEndpointNotMarkedOpenApi_Returns403()
    {
        // guards the stub: a token caller is really authenticated through the OpenApi scheme
        var response = await TokenClient(Enabled()).GetAsync("/api/v1/basic/authorized");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FlagOn_TokenCaller_ReturnsBuiltCapabilities()
    {
        var response = await TokenClient(Enabled()).GetAsync(Uri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<VaultCapabilitiesVm>>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal(["atomic-secrets"], body.Data!.Capabilities);
        Assert.False(string.IsNullOrWhiteSpace(body.Data.FeatbitVersion));
        Assert.False(string.IsNullOrWhiteSpace(body.Data.ForkRevision));
    }

    [Fact]
    public async Task FlagOn_JwtCaller_Returns200()
    {
        var client = await _app.CreateAuthenticatedClientAsync(Enabled(), includeTenantContext: true);

        var response = await client.GetAsync(Uri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FlagOn_ResponseUsesCamelCaseEnvelope()
    {
        var response = await TokenClient(Enabled()).GetAsync(Uri);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.String, data.GetProperty("featbitVersion").ValueKind);
        Assert.Equal(JsonValueKind.String, data.GetProperty("forkRevision").ValueKind);
        Assert.Equal(JsonValueKind.Array, data.GetProperty("capabilities").ValueKind);
    }

    [Fact]
    public async Task FlagOn_ConfiguredVersionAndRevision_AreReported()
    {
        var factory = Enabled(
            ("VaultIntegration:FeatBitVersion", "6.1.0"),
            ("VaultIntegration:ForkRevision", "abc1234"));

        var body = await (await TokenClient(factory).GetAsync(Uri))
            .Content.ReadFromJsonAsync<ApiResponse<VaultCapabilitiesVm>>(JsonOptions);

        Assert.Equal("6.1.0", body!.Data!.FeatbitVersion);
        Assert.Equal("abc1234", body.Data.ForkRevision);
    }

    [Fact]
    public async Task FlagOn_EvaluationServerDeclared_DoesNotReportCloseOnRevokeBeforeItIsBuilt()
    {
        var factory = Enabled(("VaultIntegration:EvaluationServerEnabled", "true"));

        var body = await (await TokenClient(factory).GetAsync(Uri))
            .Content.ReadFromJsonAsync<ApiResponse<VaultCapabilitiesVm>>(JsonOptions);

        Assert.DoesNotContain(VaultCapabilities.CloseOnRevoke, body!.Data!.Capabilities);
    }

    [Fact]
    public async Task FlagOff_EndpointIsNotInOpenApiDocument()
    {
        var response = await App().CreateClient().GetAsync("/openapi/OpenApi.json");

        Assert.DoesNotContain("vault-integration", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FlagOn_EndpointIsInOpenApiDocument()
    {
        var response = await Enabled().CreateClient().GetAsync("/openapi/OpenApi.json");

        Assert.Contains("/api/v1/vault-integration/capabilities", await response.Content.ReadAsStringAsync());
    }
}
