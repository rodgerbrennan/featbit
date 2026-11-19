namespace Api.Authorization;

/// <summary>
/// Set on <see cref="HttpContext.Items"/> when an authorization denial means the target does not exist for the
/// caller. <see cref="ApiAuthorizationResultHandler"/> then answers 404 instead of 403. Only vault-integration
/// code paths set it, and only with <c>VaultIntegration:Enabled</c> on.
/// </summary>
public static class ResourceNotFoundMarker
{
    public const string Key = "featbit.vault.resource-not-found";

    public static void Mark(HttpContext context) => context.Items[Key] = true;

    public static bool IsMarked(HttpContext context) => context.Items.ContainsKey(Key);
}
