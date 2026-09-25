using System.Text.Json;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging.Authentication;

public class EntraTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<EntraOptions> options,
    ILogger<EntraTokenProvider> logger,
    IAmazonSecurityTokenService sts
) : IEntraTokenProvider
{
    public async Task<(string AccessToken, DateTimeOffset ExpiresOn)> ExchangeForAccessTokenAsync(
        string scope,
        CancellationToken cancellationToken = default
    )
    {
        var opts = options.Value ?? throw new InvalidOperationException("EntraOptions not configured");
        var tenant = opts.TenantId ?? throw new InvalidOperationException("EntraOptions:TenantId is required");
        var clientId = opts.ClientId ?? throw new InvalidOperationException("EntraOptions:ClientId is required");

        var awsJwt = await GetWebIdentityTokenAsync(cancellationToken).ConfigureAwait(false);

        var client = httpClientFactory.CreateClient("EntraTokenExchange");
        var url = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token";

        var form = new List<KeyValuePair<string, string>>
        {
            new("client_id", clientId),
            new("scope", string.IsNullOrEmpty(scope) ? opts.Scope : scope),
            new("grant_type", "client_credentials"),
            new("client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
            new("client_assertion", awsJwt),
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new FormUrlEncodedContent(form);

        using var res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var content = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!res.IsSuccessStatusCode)
        {
            logger.LogError("Entra token exchange failed: {Status} {Body}", res.StatusCode, content);
            throw new InvalidOperationException("Failed to exchange token with Entra");
        }

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        var expiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 300;
        var expiresOn = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 30);

        return (accessToken!, expiresOn);
    }

    private async Task<string> GetWebIdentityTokenAsync(CancellationToken cancellationToken = default)
    {
        var req = new GetWebIdentityTokenRequest();
        var res = await sts.GetWebIdentityTokenAsync(req, cancellationToken).ConfigureAwait(false);

        return res?.WebIdentityToken
            ?? throw new InvalidOperationException("Failed to obtain web identity token from AWS STS.");
    }
}
