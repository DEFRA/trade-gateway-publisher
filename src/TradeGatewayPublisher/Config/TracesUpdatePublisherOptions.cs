using System.ComponentModel.DataAnnotations;

namespace TradeGatewayPublisher.Config;

public class TracesUpdatePublisherOptions
{
    public const string SectionName = "TracesUpdatePublisher";

    [Required]
    public required string TopicArn { get; init; }
}
