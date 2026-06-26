using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Messaging;

[ExcludeFromCodeCoverage]
public class FlociOptions
{
    [ConfigurationKeyName("AWS_ACCESS_KEY_ID")]
    public string? AccessKeyId { get; init; }

    [ConfigurationKeyName("AWS_REGION")]
    public string? AwsRegion { get; init; }

    [ConfigurationKeyName("AWS_SECRET_ACCESS_KEY")]
    public string? SecretAccessKey { get; init; }

    [ConfigurationKeyName("SNS_ENDPOINT")]
    public string? SnsEndpoint { get; init; }

    [ConfigurationKeyName("SQS_ENDPOINT")]
    public string? SqsEndpoint { get; init; }

    [ConfigurationKeyName("USE_FLOCI")]
    public bool? UseFloci { get; init; } = false;
}
