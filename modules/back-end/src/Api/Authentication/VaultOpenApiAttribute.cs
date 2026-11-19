namespace Api.Authentication;

/// <summary>
/// Vault integration change 1: opens an action to access-token callers, and lists it in the public OpenAPI
/// document, only when <c>VaultIntegration:Enabled</c> is on. With the flag off the action behaves exactly as
/// upstream: token callers get 403 and the document does not list it. Marks only the actions the vault plugin
/// calls.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class VaultOpenApiAttribute : Attribute
{
}
