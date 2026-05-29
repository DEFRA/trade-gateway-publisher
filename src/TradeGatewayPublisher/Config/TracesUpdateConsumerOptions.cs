namespace TradeGatewayPublisher.Config;

using System.ComponentModel.DataAnnotations;

public class TracesUpdateConsumerOptions
{
    public const string SectionName = "TracesUpdateConsumer";

    [Required]
    public required string IntraQueueUrl { get; init; }
}
