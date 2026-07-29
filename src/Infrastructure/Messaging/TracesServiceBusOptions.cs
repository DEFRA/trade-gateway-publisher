using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Messaging;

[ExcludeFromCodeCoverage]
public class TracesServiceBusOptions
{
    public const string SectionName = "TracesServiceBus";

    public required ServiceBusQueue Ched { get; init; }

    public required ServiceBusQueue Intra { get; init; }
}

[ExcludeFromCodeCoverage]
public class ServiceBusQueue
{
    public required string ConnectionString { get; init; }

    public required string QueueName { get; init; }
}
