namespace Application.VaultIntegration;

public class VaultCapabilitiesVm
{
    public string FeatbitVersion { get; set; }

    public string ForkRevision { get; set; }

    public string[] Capabilities { get; set; } = [];
}
