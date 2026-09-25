using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Messaging.Authentication;

public interface IEntraTokenProvider
{
    /// <summary>
    /// Exchange an AWS OIDC JWT (obtained via IAwsOidcTokenProvider) for a Microsoft Entra access token.
    /// Returns the token string and its expiry time.
    /// </summary>
    Task<(string AccessToken, DateTimeOffset ExpiresOn)> ExchangeForAccessTokenAsync(
        string scope,
        CancellationToken cancellationToken = default
    );
}
