using Api.Setup;
using Application.VaultIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.UnitTests.Setup;

/// <summary>Change 7: the Control Plane binds <c>VaultIntegration</c>, default off.</summary>
public sealed class ServicesRegisterVaultIntegrationTests
{
    private static VaultIntegrationOptions Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();

        ServicesRegister.AddVaultIntegration(services, configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<VaultIntegrationOptions>>().Value;
    }

    [Fact]
    public void Unset_DefaultsToDisabled()
    {
        Assert.False(Resolve([]).Enabled);
    }

    [Fact]
    public void Enabled_IsBound()
    {
        Assert.True(Resolve(new() { ["VaultIntegration:Enabled"] = "true" }).Enabled);
    }
}
