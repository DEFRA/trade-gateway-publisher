using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Trade.Gateway.Api.Client.Clients;

namespace TradeGatewayPublisher.Health;

[ExcludeFromCodeCoverage]
public class TracesGatewayHealthCheck(ITracesGatewayClient tracesGateway) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await tracesGateway.HealthCheck(cancellationToken);

            if (response.StatusCode is not HttpStatusCode.OK)
                throw new InvalidOperationException($"Unexpected HTTP status code: {response.StatusCode}");

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                exception: new Exception($"Failed to connect to Traces Gateway", ex)
            );
        }
    }
}
