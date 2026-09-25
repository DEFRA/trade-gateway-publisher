using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging.Authentication;

/// <summary>
/// TokenCredential implementation that requests an access token from Microsoft Entra by exchanging an AWS OIDC JWT.
/// Caches the token until near expiry.
/// </summary>
public class EntraTokenCredential(
    IEntraTokenProvider provider,
    IOptions<EntraOptions> options,
    ILogger<EntraTokenCredential> logger
) : TokenCredential
{
    private readonly EntraOptions _options = options.Value ?? throw new ArgumentNullException(nameof(options));

    private readonly SemaphoreSlim _lock = new(1, 1);

    private AccessToken _cached;

    private const int ClockSkewMarginSeconds = 60;

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();
    }

    public override async ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken
    )
    {
        var scope = requestContext.Scopes.Length > 0 ? requestContext.Scopes[0] : _options.Scope;

        var tokenExpirationCutoff = DateTimeOffset.UtcNow.AddSeconds(ClockSkewMarginSeconds);

        if (_cached.ExpiresOn > tokenExpirationCutoff)
            return _cached;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // retry after obtaining the lock to ensure that another thread hasn't since obtained the token
            if (_cached.ExpiresOn > tokenExpirationCutoff)
                return _cached;

            logger.LogInformation("Obtaining Entra access token");

            var (token, expiresOn) = await provider
                .ExchangeForAccessTokenAsync(scope, cancellationToken)
                .ConfigureAwait(false);
            _cached = new AccessToken(token, expiresOn);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }
}
