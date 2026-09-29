using Infrastructure.Messaging;
using Infrastructure.Messaging.Authentication;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Infrastructure.Tests.Messaging;

public class ClientAssertionCredentialFactoryTests
{
    [Fact]
    public void Create_uses_proxy_client_when_enabled()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());

        var options = Options.Create(new CdpOptions { CdpHttpsProxy = "http://127.0.0.1:3128" });

        var factory = new ClientAssertionCredentialFactory(httpClientFactory, options);

        var cred = factory.Create("t", "c", _ => Task.FromResult("jwt"));

        Assert.NotNull(cred);

        // Verify proxy client was requested
        httpClientFactory.Received(1).CreateClient(HttpClientNames.Proxy);
    }

    [Fact]
    public void Create_does_not_use_proxy_client_when_disabled()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());

        var options = Options.Create(new CdpOptions { CdpHttpsProxy = null });

        var factory = new ClientAssertionCredentialFactory(httpClientFactory, options);

        var cred = factory.Create("t", "c", _ => Task.FromResult("jwt"));

        Assert.NotNull(cred);

        // Verify proxy client was not requested
        httpClientFactory.DidNotReceive().CreateClient(HttpClientNames.Proxy);
    }
}
