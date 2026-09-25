using System.Diagnostics.CodeAnalysis;

namespace Infrastructure.Messaging;

[ExcludeFromCodeCoverage]
public class TracesServiceBusOptions
{
    public const string SectionName = "TracesServiceBus";

    // Optional connection string used for development/emulator scenarios (Shared Access Key)
    public string? ConnectionString { get; init; }

    public required ServiceBusTopic Ched { get; init; }

    public required ServiceBusTopic Intra { get; init; }

    public EntraOptions? EntraOptions { get; set; }
}

[ExcludeFromCodeCoverage]
public class ServiceBusTopic
{
    public required string TopicName { get; init; }
}

public class EntraOptions
{
    // Fully qualified namespace to use for TokenCredential-based clients (e.g. "my-namespace.servicebus.windows.net")
    public required string Namespace { get; init; }

    public required string TenantId { get; init; }

    public required string ClientId { get; init; }

    public required string Scope { get; init; }
}
