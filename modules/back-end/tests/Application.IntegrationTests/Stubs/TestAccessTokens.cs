using System.Linq.Expressions;
using Application.Services;
using Domain.AccessTokens;
using Domain.Policies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Application.IntegrationTests.Stubs;

/// <summary>
/// Lets a test call the API as an access-token (OpenApi scheme) caller, without a database:
/// <see cref="IAccessTokenService"/> is replaced so <c>OpenApiHandler</c> finds one active Service token.
/// Send <see cref="Token"/> as the raw <c>Authorization</c> header value (no <c>Bearer</c> prefix).
/// </summary>
public static class TestAccessTokens
{
    public const string Token = "api-test-service-token-0000000000000000000000";

    public static AccessToken ServiceToken(PolicyStatement[]? permissions = null) =>
        new(TestWorkspace.OrganizationId, TestUser.Id, "test-service-token", AccessTokenTypes.Service,
            permissions ?? [])
        {
            Token = Token
        };

    public static void Replace(IServiceCollection services, AccessToken? token = null)
    {
        var accessToken = token ?? ServiceToken();
        var service = new Mock<IAccessTokenService>();
        service
            .Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<AccessToken, bool>>>()))
            .ReturnsAsync((Expression<Func<AccessToken, bool>> predicate) =>
                predicate.Compile()(accessToken) ? accessToken : null);
        service.Setup(x => x.RefreshLastUsedAtAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);

        services.Replace(ServiceDescriptor.Singleton(service.Object));
    }

    public static HttpClient Authorize(HttpClient client)
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Token);
        return client;
    }
}
