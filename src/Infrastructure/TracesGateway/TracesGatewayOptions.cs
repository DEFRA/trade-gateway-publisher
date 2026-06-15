using System.ComponentModel.DataAnnotations;

namespace Infrastructure.TracesGateway;

public class TracesGatewayOptions
{
    public const string SectionName = "TracesGateway";

    [Required]
    public required string BaseUrl { get; init; }

    [Required]
    public string Audience { get; init; } = "trade-gateway";

    public int DurationSeconds { get; init; } = 3600;
}
