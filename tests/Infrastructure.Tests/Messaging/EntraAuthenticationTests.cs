using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Azure.Core;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Infrastructure.Tests.Messaging;

public class EntraAuthenticationTests
{
    private static readonly string[] s_contextScopes = ["scopes"];

    [Fact]
    public void ClientAssertionCredentialFactory_creates_credential()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var factory = new ClientAssertionCredentialFactory(httpClientFactory, Options.Create(new CdpOptions()));
        var cred = factory.Create("t", "c", _ => Task.FromResult("jwt"));
        Assert.NotNull(cred);
        Assert.IsType<TokenCredential>(cred, false);
        // Type name sanity check (avoid strong dependency on Azure.Identity public type)
        Assert.Equal("ClientAssertionCredential", cred.GetType().Name);
    }

    [Fact]
    public async Task EntraTokenProvider_uses_assertion_callback_to_get_web_identity_token()
    {
        var sts = Substitute.For<IAmazonSecurityTokenService>();
        // Return a well-formed JWT so the diagnostic parsing in EntraTokenProvider can read claims
        var handlerStub = new JwtSecurityTokenHandler();
        var jwtStub = new JwtSecurityToken(
            issuer: "https://issuer.tokens.sts.global.api.aws",
            audience: "api://audience",
            claims: [new Claim("sub", "arn:aws:iam::123456:role/rolename")],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(5)
        );
        var tokenString = handlerStub.WriteToken(jwtStub);

        sts.GetWebIdentityTokenAsync(Arg.Any<GetWebIdentityTokenRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetWebIdentityTokenResponse { WebIdentityToken = tokenString }));

        var options = Options.Create(
            new EntraOptions
            {
                Namespace = "ns",
                TenantId = "t",
                ClientId = "c",
                Scope = "s",
                Audience = "aud",
                SigningAlgorithm = "RS256",
            }
        );
        var logger = new NullLogger<EntraTokenProvider>();

        // Factory that returns a TokenCredential which invokes the provided clientAssertionCallback when GetTokenAsync is called
        var factory = Substitute.For<IClientAssertionCredentialFactory>();

        factory
            .Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<string>>>())
            .Returns(ci =>
            {
                var callback = ci.ArgAt<Func<CancellationToken, Task<string>>>(2);
                return new CallbackTokenCredential(
                    async (_, _) =>
                    {
                        var assertion = await callback(CancellationToken.None).ConfigureAwait(false);
                        return new AccessToken("returned-" + assertion, DateTimeOffset.UtcNow.AddMinutes(5));
                    }
                );
            });

        var provider = new EntraTokenProvider(options, logger, sts, factory);
        var (token, expiresOn) = await provider.ExchangeForAccessTokenAsync(CancellationToken.None);

        Assert.Equal("returned-" + tokenString, token);
        Assert.True(expiresOn > DateTimeOffset.UtcNow);
        await sts.Received(1)
            .GetWebIdentityTokenAsync(Arg.Any<GetWebIdentityTokenRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EntraTokenProvider_throws_when_sts_returns_no_token()
    {
        var sts = Substitute.For<IAmazonSecurityTokenService>();
        // STS returns no token
        sts.GetWebIdentityTokenAsync(Arg.Any<GetWebIdentityTokenRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetWebIdentityTokenResponse { WebIdentityToken = null! }));

        var options = Options.Create(
            new EntraOptions
            {
                Namespace = "ns",
                TenantId = "t",
                ClientId = "c",
                Scope = "s",
                Audience = "aud",
                SigningAlgorithm = "RS256",
            }
        );
        var logger = new NullLogger<EntraTokenProvider>();

        var factory = Substitute.For<IClientAssertionCredentialFactory>();
        factory
            .Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<string>>>())
            .Returns(ci =>
            {
                var callback = ci.ArgAt<Func<CancellationToken, Task<string>>>(2);
                return new CallbackTokenCredential(
                    async (_, _) =>
                    {
                        var assertion = await callback(CancellationToken.None).ConfigureAwait(false);
                        return new AccessToken("returned-" + assertion, DateTimeOffset.UtcNow.AddMinutes(5));
                    }
                );
            });

        var provider = new EntraTokenProvider(options, logger, sts, factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ExchangeForAccessTokenAsync(CancellationToken.None)
        );
        await sts.Received(1)
            .GetWebIdentityTokenAsync(Arg.Any<GetWebIdentityTokenRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EntraTokenCredential_GetToken_async_calls_provider_once()
    {
        var provider = Substitute.For<IEntraTokenProvider>();
        var now = DateTimeOffset.UtcNow;
        provider
            .ExchangeForAccessTokenAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(("token1", now.AddMinutes(5))));

        var logger = new NullLogger<EntraTokenCredential>();
        var credential = new EntraTokenCredential(provider, logger);

        var ctx = new TokenRequestContext(s_contextScopes);

        // Call async GetToken twice and ensure provider called once (caching)
        var t1 = await credential.GetTokenAsync(ctx, CancellationToken.None);
        var t2 = await credential.GetTokenAsync(ctx, CancellationToken.None);

        Assert.Equal(t1.Token, t2.Token);
        await provider.Received(1).ExchangeForAccessTokenAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void EntraTokenCredential_GetToken_sync_throws_NotSupported()
    {
        var provider = Substitute.For<IEntraTokenProvider>();
        var now = DateTimeOffset.UtcNow;
        provider
            .ExchangeForAccessTokenAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(("token-sync", now.AddMinutes(5))));

        var logger = new NullLogger<EntraTokenCredential>();
        var credential = new EntraTokenCredential(provider, logger);

        var ctx = new TokenRequestContext(s_contextScopes);

        // First synchronous call (wraps GetTokenAsync) should throw not supported
        Assert.Throws<NotSupportedException>(() => credential.GetToken(ctx, CancellationToken.None));
    }

    private class CallbackTokenCredential(Func<TokenRequestContext, CancellationToken, Task<AccessToken>> cb)
        : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken
        ) => new(cb(requestContext, cancellationToken));
    }
}
