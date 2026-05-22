using System.ComponentModel.DataAnnotations;

namespace Data;

public class MongoConfig
{
    [Required]
    public required string DatabaseUri { get; init; }

    [Required]
    public required string DatabaseName { get; init; }
}
