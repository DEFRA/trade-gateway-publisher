using System.Net.Http.Headers;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.Options;

namespace Infrastructure.TracesGateway;

public class StsAuthDelegatingHandler(IAmazonSecurityTokenService sts, IOptions<TracesGatewayOptions> options)
    : DelegatingHandler
{
    private string? _token;
    private DateTime _tokenExpiry = DateTime.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await GetOrRefreshTokenAsync(cancellationToken)
        );
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetOrRefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (_token != null && DateTime.UtcNow < _tokenExpiry.AddSeconds(-30))
            return _token;

        var opts = options.Value;
        var response = await sts.GetWebIdentityTokenAsync(
            new GetWebIdentityTokenRequest
            {
                Audience = [opts.Audience],
                DurationSeconds = opts.DurationSeconds,
                SigningAlgorithm = "RS256",
            },
            cancellationToken
        );

        _token = response.WebIdentityToken;
        _tokenExpiry = response.Expiration ?? DateTime.UtcNow.AddSeconds(opts.DurationSeconds);
        return _token;
    }
}
