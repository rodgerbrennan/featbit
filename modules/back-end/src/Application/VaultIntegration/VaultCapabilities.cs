namespace Application.VaultIntegration;

/// <summary>
/// The fork changes the vault plugin checks for. A capability is reported only once its change is built:
/// each fork change adds its own name to <see cref="Built"/>.
/// </summary>
public static class VaultCapabilities
{
    public const string OpenApiSecrets = "openapi-secrets";
    public const string ExpiresAt = "expires-at";
    public const string SubsetCheck = "subset-check";
    public const string CloseOnRevoke = "close-on-revoke";
    public const string AtomicSecrets = "atomic-secrets";
    public const string DeletedEnv404 = "deleted-env-404";
    public const string OrgScopedEnv = "org-scoped-env";

    public static string[] Built(VaultIntegrationOptions options) =>
    [
        // change 5
        AtomicSecrets,
        // change 8
        OrgScopedEnv,
        // change 1
        OpenApiSecrets
    ];
}
