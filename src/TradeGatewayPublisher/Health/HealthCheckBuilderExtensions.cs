using System.Diagnostics.CodeAnalysis;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TradeGatewayPublisher.Health;

[ExcludeFromCodeCoverage]
public static class HealthCheckBuilderExtensions
{
    public static IHealthChecksBuilder AddSns(
        this IHealthChecksBuilder builder,
        string name,
        Func<IServiceProvider, string> queueNameFunc,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null
    )
    {
        builder.Add(
            new HealthCheckRegistration(
                name,
                sp => new SnsHealthCheck(sp.GetRequiredService<IAmazonSimpleNotificationService>(), queueNameFunc(sp)),
                HealthStatus.Unhealthy,
                tags,
                timeout
            )
        );

        return builder;
    }

    public static IHealthChecksBuilder AddSqs(
        this IHealthChecksBuilder builder,
        string queueUrl,
        Func<IServiceProvider, string> queueNameFunc,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null
    )
    {
        builder.Add(
            new HealthCheckRegistration(
                queueUrl,
                sp => new SqsHealthCheck(sp.GetRequiredService<IAmazonSQS>(), queueNameFunc(sp)),
                HealthStatus.Unhealthy,
                tags,
                timeout
            )
        );

        return builder;
    }
}
