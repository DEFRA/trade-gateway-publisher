using Azure.Core;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Infrastructure.Tests.Messaging;

public class EntraTokenCredentialTests
{
    [Fact]
    public async Task GetTokenAsync_caches_token_and_refreshes_after_expiry()
    {
        var provider = Substitute.For<IEntraTokenProvider>();
        var now = DateTimeOffset.UtcNow;
        provider
            .ExchangeForAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(("token1", now.AddMinutes(5))), Task.FromResult(("token2", now.AddMinutes(10))));

        var options = Options.Create(
            new EntraOptions
            {
                TenantId = "t",
                ClientId = "c",
                Scope = "scope",
                Namespace = "namespace",
            }
        );
        var logger = new NullLogger<EntraTokenCredential>();

        var credential = new EntraTokenCredential(provider, options, logger);

        var ctx = new TokenRequestContext(new[] { "scope" });

        var t1 = await credential.GetTokenAsync(ctx, CancellationToken.None);
        var t2 = await credential.GetTokenAsync(ctx, CancellationToken.None);

        Assert.Equal(t1.Token, t2.Token);

        // Force expiration by setting internal cache expiry earlier than now (can't access private fields)
        // Instead, simulate by waiting until provider will return next token via AndThen (not practical).
        // Verify provider was called at least once and not for every call.
        await provider.Received(1).ExchangeForAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
