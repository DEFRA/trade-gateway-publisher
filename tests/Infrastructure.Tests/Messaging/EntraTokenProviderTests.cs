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
                        Expiration = System.DateTime.UtcNow.AddHours(1),
                    }
                )
            );

        // Create a fake HttpMessageHandler that returns a successful token response
        var handler = new TestHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"entra-token\", \"expires_in\": 3600}"),
            }
        );

        var httpClient = new HttpClient(handler);
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient("EntraTokenExchange").Returns(httpClient);

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

        var provider = new EntraTokenProvider(httpFactory, options, logger, sts);

        var (token, expiresOn) = await provider.ExchangeForAccessTokenAsync("scope", CancellationToken.None);

        Assert.Equal("entra-token", token);
        Assert.True(expiresOn > DateTimeOffset.UtcNow);
    }

    private class TestHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(response);
        }
    }
}
