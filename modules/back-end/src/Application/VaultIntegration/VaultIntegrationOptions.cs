namespace Application.VaultIntegration;

/// <summary>
/// Settings for the vault-plugin-secrets-featbit integration (section <c>VaultIntegration</c>, or
/// <c>VaultIntegration__*</c> environment variables). With <see cref="Enabled"/> off, the default, FeatBit
/// behaves exactly like upstream.
/// </summary>
public class VaultIntegrationOptions
{
    public const string SectionName = "VaultIntegration";

    /// <summary>
    /// Turns on the gated fork changes and the capabilities endpoint. Read at startup: restart to change it.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The operator's declaration that the evaluation servers run with <c>VaultIntegration:Enabled</c>.
    /// The API server cannot observe them, so capabilities that depend on them rely on this setting.
    /// </summary>
    public bool EvaluationServerEnabled { get; set; }

    /// <summary>
    /// Overrides the reported FeatBit version. Defaults to the image's <c>VERSION</c> variable, then the API
    /// assembly's version.
    /// </summary>
    public string FeatBitVersion { get; set; }

    /// <summary>Overrides the reported fork revision. Defaults to the source revision the API was built from.</summary>
    public string ForkRevision { get; set; }
}
