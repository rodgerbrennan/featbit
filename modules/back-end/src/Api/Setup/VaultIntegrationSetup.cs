using Api.Controllers;
using Application.VaultIntegration;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Api.Setup;

public static class VaultIntegrationSetup
{
    /// <summary>
    /// Binds <see cref="VaultIntegrationOptions"/>. With the flag off, also removes the vault-integration
    /// controller, so its route and OpenAPI entry do not exist, as on upstream FeatBit.
    /// </summary>
    public static IMvcBuilder AddVaultIntegration(this IMvcBuilder mvc, IConfiguration configuration)
    {
        var section = configuration.GetSection(VaultIntegrationOptions.SectionName);
        mvc.Services.Configure<VaultIntegrationOptions>(section);

        var enabled = section.Get<VaultIntegrationOptions>()?.Enabled ?? false;
        if (!enabled)
        {
            mvc.ConfigureApplicationPartManager(manager =>
                manager.FeatureProviders.Add(new ExcludeVaultIntegrationController()));
        }

        return mvc;
    }

    // runs after the default ControllerFeatureProvider, so it removes what that one found
    private sealed class ExcludeVaultIntegrationController : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            var controller = feature.Controllers.FirstOrDefault(x => x.AsType() == typeof(VaultIntegrationController));
            if (controller != null)
            {
                feature.Controllers.Remove(controller);
            }
        }
    }
}
