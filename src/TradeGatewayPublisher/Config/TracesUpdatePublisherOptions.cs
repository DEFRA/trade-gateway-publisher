using System.ComponentModel.DataAnnotations;

namespace TradeGatewayPublisher.Config;

public class TracesUpdatePublisherOptions
{
    public const string SectionName = "TracesUpdatePublisher";

    [Required]
    public required string IntraInternalTopicArn { get; init; }

    [Required]
    public required string IntraTopicArn { get; init; }
}
