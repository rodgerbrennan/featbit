using Domain.VaultIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Application.IntegrationTests.Configuration;

/// <summary>Change 7: the evaluation server binds <c>VaultIntegration</c>, default off.</summary>
[Trait("Category", "Host")]
[Collection(nameof(TestApp))]
public class VaultIntegrationOptionsTests
{
    private readonly TestApp _app;

    public VaultIntegrationOptionsTests(TestApp app)
    {
        _app = app;
    }

    [Fact]
    public void Unset_DefaultsToDisabled()
    {
        var options = _app.Services.GetRequiredService<IOptions<VaultIntegrationOptions>>().Value;

        Assert.False(options.Enabled);
    }

    [Fact]
    public void Enabled_IsBound()
    {
        var app = _app.WithSettings(("VaultIntegration:Enabled", "true"));

        var options = app.Services.GetRequiredService<IOptions<VaultIntegrationOptions>>().Value;

        Assert.True(options.Enabled);
    }
}
