using System.Diagnostics.CodeAnalysis;

namespace Infrastructure.Messaging;

[ExcludeFromCodeCoverage]
public class TracesServiceBusOptions
{
    public const string SectionName = "TracesServiceBus";

    public required ServiceBusTopic Ched { get; init; }

    public required ServiceBusTopic Intra { get; init; }
}

[ExcludeFromCodeCoverage]
public class ServiceBusTopic
{
    public required string ConnectionString { get; init; }

    public required string TopicName { get; init; }
}
