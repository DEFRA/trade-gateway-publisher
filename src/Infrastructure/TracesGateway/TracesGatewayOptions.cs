using System.ComponentModel.DataAnnotations;

namespace Infrastructure.TracesGateway;

public class TracesGatewayOptions
{
    public const string SectionName = "TracesGateway";

    [Required]
    public required string BaseUrl { get; init; }
}
