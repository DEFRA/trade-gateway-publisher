using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging.Authentication;

public class EntraTokenProvider(
    IOptions<EntraOptions> options,
    ILogger<EntraTokenProvider> logger,
    IAmazonSecurityTokenService sts,
    IClientAssertionCredentialFactory clientAssertionCredentialFactory
) : IEntraTokenProvider
{
    public async Task<(string AccessToken, DateTimeOffset ExpiresOn)> ExchangeForAccessTokenAsync(
        CancellationToken cancellationToken = default
    )
    {
        var entraOptions = options.Value ?? throw new InvalidOperationException("EntraOptions not configured");

        var assertionCallback = new Func<CancellationToken, Task<string>>(GetWebIdentityTokenAsync);

        var credential = clientAssertionCredentialFactory.Create(
            entraOptions.TenantId,
            entraOptions.ClientId,
            assertionCallback
        );

        var tokenRequest = new TokenRequestContext([entraOptions.Scope]);

        var accessToken = await credential.GetTokenAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Obtained a new access token - expiration : {ExpiresOn}", accessToken.ExpiresOn);
        return (accessToken.Token, accessToken.ExpiresOn);
    }

    private async Task<string> GetWebIdentityTokenAsync(CancellationToken cancellationToken = default)
    {
        var req = new GetWebIdentityTokenRequest();
        var res = await sts.GetWebIdentityTokenAsync(req, cancellationToken).ConfigureAwait(false);

        return res?.WebIdentityToken
            ?? throw new InvalidOperationException("Failed to obtain web identity token from AWS STS.");
    }
}
