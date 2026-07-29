using Microsoft.Extensions.Configuration;

namespace Infrastructure.Messaging;

public class CdpOptions
{
    [ConfigurationKeyName("CDP_HTTPS_PROXY")]
    public string? CdpHttpsProxy { get; init; }

    public bool IsProxyEnabled => !string.IsNullOrWhiteSpace(CdpHttpsProxy);
}
