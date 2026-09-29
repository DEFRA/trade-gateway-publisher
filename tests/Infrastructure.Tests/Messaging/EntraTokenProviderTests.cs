using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Azure.Core;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using static NSubstitute.Arg;

namespace Infrastructure.Tests.Messaging;

public class EntraTokenProviderTests
{
    [Fact]
    public async Task ExchangeForAccessTokenAsync_posts_client_assertion_and_returns_token()
    {
        var tokenExpirationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var sts = Substitute.For<IAmazonSecurityTokenService>();
        sts.GetWebIdentityTokenAsync(Any<GetWebIdentityTokenRequest>(), Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    new GetWebIdentityTokenResponse { WebIdentityToken = "aws-jwt", Expiration = tokenExpirationDate }
                )
            );

        var options = Options.Create(
            new EntraOptions
            {
                Namespace = "test-namespace.servicebus.windows.net",
                TenantId = "tenant",
                ClientId = "client",
                Scope = "scope",
                Audience = "aud",
                SigningAlgorithm = "RS256",
            }
        );
        var logger = Substitute.For<ILogger<EntraTokenProvider>>();

        // Fake TokenCredential that returns a known access token
        var fakeCredential = new FakeTokenCredential("entra-token", tokenExpirationDate);

        var fakeFactory = new FakeClientAssertionCredentialFactory(fakeCredential);

        // capture the log state as logger uses internal FormattedLogValues - https://github.com/nsubstitute/NSubstitute/issues/597
        object? capturedState = null;
        logger
            .When(x =>
                x.Log(
                    Any<LogLevel>(),
                    Any<EventId>(),
                    Any<object>(),
                    Any<Exception>(),
                    Any<Func<object, Exception?, string>>()
                )
            )
            .Do(ci => capturedState = ci.ArgAt<object>(2));

        var provider = new EntraTokenProvider(options, logger, sts, fakeFactory);

        var (token, expiresOn) = await provider.ExchangeForAccessTokenAsync(CancellationToken.None);

        Assert.Equal("entra-token", token);
        Assert.Equal(tokenExpirationDate, expiresOn);

        Assert.NotNull(capturedState);
        Assert.Contains("Obtained a new access token", capturedState.ToString());
    }

    private class FakeTokenCredential(string value, DateTimeOffset expires) : TokenCredential
    {
        private readonly AccessToken _token = new(value, expires);

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            _token;

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken
        ) => new(_token);
    }

    private class FakeClientAssertionCredentialFactory(TokenCredential credential) : IClientAssertionCredentialFactory
    {
        public TokenCredential Create(
            string tenantId,
            string clientId,
            Func<CancellationToken, Task<string>> clientAssertionCallback
        ) => credential;
    }
}
