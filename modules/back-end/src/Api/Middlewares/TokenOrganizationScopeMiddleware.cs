using Api.Authorization;
using Api.Controllers;
using Application.Bases;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Api.Middlewares;

/// <summary>
/// Vault integration change 8: answers 404 when an access-token caller routes to an environment or project of
/// another organization. Runs before authorization, so it also covers endpoints that check permissions in their
/// handlers or not at all. A route parameter is an environment ID when it directly follows an <c>envs</c>
/// segment and a project ID when it directly follows <c>projects</c>, whatever the parameter is named
/// (<c>envs/{envId}</c>, <c>projects/{id}</c>, <c>projects/{projectId}/envs/{id}</c>).
/// </summary>
public class TokenOrganizationScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITokenOrganizationScope scope)
    {
        if (context.GetEndpoint() is RouteEndpoint endpoint)
        {
            foreach (var (kind, parameter) in ScopedParameters(endpoint.RoutePattern))
            {
                if (!Guid.TryParse(context.Request.RouteValues[parameter]?.ToString(), out var id))
                {
                    continue;
                }

                var foreign = kind == ScopedKind.Env
                    ? await scope.IsForeignEnvAsync(context, id)
                    : await scope.IsForeignProjectAsync(context, id);
                if (foreign)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsJsonAsync(ApiResponse<object>.Error(ErrorCodes.ResourceNotFound));

                    return;
                }
            }
        }

        await next(context);
    }

    private enum ScopedKind
    {
        Env,
        Project
    }

    private static IEnumerable<(ScopedKind Kind, string Parameter)> ScopedParameters(RoutePattern pattern)
    {
        // any parameter named like an environment ID, e.g. {targetEnvId}
        foreach (var parameter in pattern.Parameters)
        {
            if (parameter.Name.EndsWith("EnvId", StringComparison.OrdinalIgnoreCase))
            {
                yield return (ScopedKind.Env, parameter.Name);
            }
        }

        var segments = pattern.PathSegments;
        for (var i = 1; i < segments.Count; i++)
        {
            if (segments[i - 1].Parts is not [RoutePatternLiteralPart literal] ||
                segments[i].Parts is not [RoutePatternParameterPart parameter])
            {
                continue;
            }

            if (string.Equals(literal.Content, "envs", StringComparison.OrdinalIgnoreCase))
            {
                yield return (ScopedKind.Env, parameter.Name);
            }
            else if (string.Equals(literal.Content, "projects", StringComparison.OrdinalIgnoreCase))
            {
                yield return (ScopedKind.Project, parameter.Name);
            }
        }
    }
}
