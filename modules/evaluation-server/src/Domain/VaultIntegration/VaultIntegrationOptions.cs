namespace Domain.VaultIntegration;

/// <summary>
/// Settings for the vault-plugin-secrets-featbit integration (section <c>VaultIntegration</c>, or
/// <c>VaultIntegration__*</c> environment variables). With <see cref="Enabled"/> off, the default, the
/// evaluation server behaves exactly like upstream. Set it on the API server, the evaluation servers and the
/// Control Plane alike.
/// </summary>
public class VaultIntegrationOptions
{
    public const string SectionName = "VaultIntegration";

    /// <summary>Turns on the gated fork changes in the evaluation server.</summary>
    public bool Enabled { get; set; }
}
