using System.Reflection;
using Api.Authentication;
using Application.VaultIntegration;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

/// <summary>
/// Reports which fork changes the vault plugin can rely on. Registered only when
/// <c>VaultIntegration:Enabled</c> is on (see <c>VaultIntegrationSetup</c>), so with the flag off the route
/// does not exist, exactly as on upstream FeatBit.
/// </summary>
[Route("api/v{version:apiVersion}/vault-integration")]
public class VaultIntegrationController(IOptions<VaultIntegrationOptions> options, IConfiguration configuration)
    : ApiControllerBase
{
    private static readonly string InformationalVersion =
        typeof(VaultIntegrationController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

    /// <summary>
    /// Get the vault integration capabilities
    /// </summary>
    /// <remarks>
    /// Lists the fork changes this API server has built and enabled. Any authenticated caller may read it.
    /// </remarks>
    [OpenApi]
    [HttpGet("capabilities")]
    public ApiResponse<VaultCapabilitiesVm> GetCapabilities()
    {
        var settings = options.Value;

        // the informational version is "<version>+<source revision>" when the build knows its revision
        var parts = InformationalVersion.Split('+', 2);
        var vm = new VaultCapabilitiesVm
        {
            // official images set the release in the VERSION environment variable (deploy/Dockerfile)
            FeatbitVersion = Choose(settings.FeatBitVersion, Choose(configuration["VERSION"], parts[0])),
            ForkRevision = Choose(settings.ForkRevision, parts.Length > 1 ? parts[1] : null),
            Capabilities = VaultCapabilities.Built(settings)
        };

        return Ok(vm);
    }

    private static string Choose(string configured, string built) =>
        !string.IsNullOrWhiteSpace(configured) ? configured
        : !string.IsNullOrWhiteSpace(built) ? built
        : "unknown";
}
