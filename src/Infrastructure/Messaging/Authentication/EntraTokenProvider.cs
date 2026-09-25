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
    private readonly ILogger<EntraTokenProvider> _logger = logger;

    public async Task<(string AccessToken, DateTimeOffset ExpiresOn)> ExchangeForAccessTokenAsync(
        string scope,
        CancellationToken cancellationToken = default
    )
    {
        var opts = options.Value ?? throw new InvalidOperationException("EntraOptions not configured");

        var assertionCallback = new Func<CancellationToken, Task<string>>(GetWebIdentityTokenAsync);

        var credential = clientAssertionCredentialFactory.Create(opts.TenantId!, opts.ClientId!, assertionCallback);

        var tokenScope = string.IsNullOrEmpty(scope) ? opts.Scope : scope;
        var tokenRequest = new TokenRequestContext([tokenScope]);

        var accessToken = await credential.GetTokenAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Obtained a new access token - expiration : {ExpiresOn}", accessToken.ExpiresOn);
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
