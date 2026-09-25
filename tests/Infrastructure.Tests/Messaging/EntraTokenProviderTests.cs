using System.Net;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Infrastructure.Tests.Messaging;

public class EntraTokenProviderTests
{
    [Fact]
    public async Task ExchangeForAccessTokenAsync_posts_client_assertion_and_returns_token()
    {
        var sts = Substitute.For<IAmazonSecurityTokenService>();
        sts.GetWebIdentityTokenAsync(Arg.Any<GetWebIdentityTokenRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    new GetWebIdentityTokenResponse
                    {
                        WebIdentityToken = "aws-jwt",
                        Expiration = DateTime.UtcNow.AddHours(1),
                    }
                )
            );

        var options = Options.Create(
            new EntraOptions
            {
                Namespace = "test-namespace.servicebus.windows.net",
                TenantId = "tenant",
                ClientId = "client",
                Scope = "scope",
            }
        );
        var logger = new NullLogger<EntraTokenProvider>();

        // Fake TokenCredential that returns a known access token
        var fakeCredential = new FakeTokenCredential("entra-token", DateTimeOffset.UtcNow.AddHours(1));

        var fakeFactory = new FakeClientAssertionCredentialFactory(fakeCredential);
        var provider = new EntraTokenProvider(options, logger, sts, fakeFactory);

        var (token, expiresOn) = await provider.ExchangeForAccessTokenAsync("scope", CancellationToken.None);

        Assert.Equal("entra-token", token);
        Assert.True(expiresOn > DateTimeOffset.UtcNow);
    }

    private class FakeTokenCredential(string value, DateTimeOffset expires) : Azure.Core.TokenCredential
    {
        private readonly Azure.Core.AccessToken _token = new(value, expires);

        public override Azure.Core.AccessToken GetToken(
            Azure.Core.TokenRequestContext requestContext,
            CancellationToken cancellationToken
        ) => _token;

        public override ValueTask<Azure.Core.AccessToken> GetTokenAsync(
            Azure.Core.TokenRequestContext requestContext,
            CancellationToken cancellationToken
        ) => new(_token);
    }

    private class FakeClientAssertionCredentialFactory(Azure.Core.TokenCredential credential)
        : IClientAssertionCredentialFactory
    {
        public Azure.Core.TokenCredential Create(
            string tenantId,
            string clientId,
            Func<CancellationToken, Task<string>> clientAssertionCallback
        ) => credential;
    }
}
