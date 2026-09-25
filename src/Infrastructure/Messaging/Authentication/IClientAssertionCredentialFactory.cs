using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Infrastructure.Messaging.Authentication;

public interface IClientAssertionCredentialFactory
{
    TokenCredential Create(
        string tenantId,
        string clientId,
        Func<CancellationToken, Task<string>> clientAssertionCallback
    );
}
