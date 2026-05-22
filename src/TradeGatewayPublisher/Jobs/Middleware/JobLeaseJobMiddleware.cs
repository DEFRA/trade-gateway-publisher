using CronJobs;
using CronJobs.Leasing;

namespace TradeGatewayPublisher.Jobs.Middleware;

public sealed class JobLeaseJobMiddleware(IJobLeaseProvider leaseProvider, ILogger<JobLeaseJobMiddleware> logger)
    : IJobMiddleware
{
    private static readonly TimeSpan s_defaultLeaseDuration = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(
        CronJobWithWatermarkJob.JobContext context,
        CancellationToken cancellationToken,
        JobExecutionDelegate next
    )
    {
        var leaseName = $"job:{context.Name}";

        await using var lease = await leaseProvider.TryAcquireAsync(
            leaseName,
            s_defaultLeaseDuration,
            cancellationToken
        );

        if (lease is null)
        {
            logger.LogInformation("Skipping {JobName} because lease could not be acquired", context.Name);

            return;
        }

        logger.LogInformation("Lease acquired for {JobName}", context.Name);

        await next(context, cancellationToken);
    }
}
