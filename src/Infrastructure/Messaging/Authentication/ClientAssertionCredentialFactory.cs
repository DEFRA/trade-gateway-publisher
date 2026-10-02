using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging.Authentication;

public class ClientAssertionCredentialFactory(IHttpClientFactory httpClientFactory, IOptions<CdpOptions> cdpOptions)
    : IClientAssertionCredentialFactory
{
    public TokenCredential Create(
        string tenantId,
        string clientId,
        Func<CancellationToken, Task<string>> clientAssertionCallback
    )
    {
        var options = new ClientAssertionCredentialOptions();

        if (cdpOptions.Value.IsProxyEnabled)
        {
            // Use the proxy-configured HttpClient so MSAL/Azure.Identity uses the same proxy
            var httpClient = httpClientFactory.CreateClient(HttpClientNames.Proxy);
            options.Transport = new HttpClientTransport(httpClient);
        }

        return new ClientAssertionCredential(tenantId, clientId, clientAssertionCallback, options);
    }
}
