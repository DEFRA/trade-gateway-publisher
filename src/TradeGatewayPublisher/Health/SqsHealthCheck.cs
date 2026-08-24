using System.Diagnostics.CodeAnalysis;
using Amazon.SQS;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TradeGatewayPublisher.Health;

[ExcludeFromCodeCoverage]
public class SqsHealthCheck(IAmazonSQS sqsClient, string queueUrl) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _ = await sqsClient.GetQueueAttributesAsync(queueUrl, ["*"], cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                exception: new Exception($"Failed to connect to AWS queue: {queueUrl}", ex)
            );
        }
    }
}
