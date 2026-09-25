using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Infrastructure.Messaging.Authentication;

public class ClientAssertionCredentialFactory : IClientAssertionCredentialFactory
{
    public TokenCredential Create(
        string tenantId,
        string clientId,
        Func<CancellationToken, Task<string>> clientAssertionCallback
    )
    {
        return new ClientAssertionCredential(tenantId, clientId, clientAssertionCallback);
    }
}
