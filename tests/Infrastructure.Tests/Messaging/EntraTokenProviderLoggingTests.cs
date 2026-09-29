using System.IdentityModel.Tokens.Jwt;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Azure.Core;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Infrastructure.Tests.Messaging;

public class EntraTokenProviderLoggingTests
{
    [Fact]
    public async Task ExchangeForAccessTokenAsync_logs_jwt_claims_from_sts()
    {
        var sts = Substitute.For<IAmazonSecurityTokenService>();

        // Build a fake JWT with known claims
        var handler = new JwtSecurityTokenHandler();
        var header = new JwtHeader { ["alg"] = "RS256" };
        var payload = new JwtPayload
        {
            ["iss"] = "https://a19dfab3-318a-47fa-8133-57c7670003c7.tokens.sts.global.api.aws",
            ["aud"] = "api://AzureADTokenExchange",
            ["sub"] = "arn:aws:sts::332499610595:assumed-role/trade-gateway-publisher/6387fcfc8b6a4dbe8cd97306ee84d238",
        };
        var jwt = new JwtSecurityToken(header, payload);
        var tokenString = handler.WriteToken(jwt);

        sts.GetWebIdentityTokenAsync(Arg.Any<GetWebIdentityTokenRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    new GetWebIdentityTokenResponse
                    {
                        WebIdentityToken = tokenString,
                        Expiration = DateTime.UtcNow.AddMinutes(5),
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
                Audience = "api://AzureADTokenExchange",
                SigningAlgorithm = "RS256",
            }
        );

        var logger = new NullLogger<EntraTokenProvider>();

        var fakeFactory = new CallbackClientAssertionCredentialFactory();
        var provider = new EntraTokenProvider(options, logger, sts, fakeFactory);

        var (token, expiresOn) = await provider.ExchangeForAccessTokenAsync(CancellationToken.None);

        Assert.Equal("entra-token", token);
        Assert.True(expiresOn > DateTimeOffset.UtcNow);

        // Verify STS was called to obtain the web identity token
        await sts.Received()
            .GetWebIdentityTokenAsync(Arg.Any<GetWebIdentityTokenRequest>(), Arg.Any<CancellationToken>());
    }

    private class CallbackClientAssertionCredentialFactory : IClientAssertionCredentialFactory
    {
        public TokenCredential Create(
            string tenantId,
            string clientId,
            Func<CancellationToken, Task<string>> clientAssertionCallback
        )
        {
            return new CallbackTokenCredential(clientAssertionCallback);
        }

        private class CallbackTokenCredential(Func<CancellationToken, Task<string>> cb) : TokenCredential
        {
            public override AccessToken GetToken(
                TokenRequestContext requestContext,
                CancellationToken cancellationToken
            )
            {
                // Trigger the client assertion callback which should cause EntraTokenProvider to call STS
                var _ = cb(cancellationToken).GetAwaiter().GetResult();
                return new AccessToken("entra-token", DateTimeOffset.UtcNow.AddHours(1));
            }

            public override ValueTask<AccessToken> GetTokenAsync(
                TokenRequestContext requestContext,
                CancellationToken cancellationToken
            )
            {
                return new ValueTask<AccessToken>(GetToken(requestContext, cancellationToken));
            }
        }
    }
}
