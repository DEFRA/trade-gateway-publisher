using Infrastructure.Leasing;
using Infrastructure.Scheduler;

namespace TradeGatewayPublisher.Jobs.Middleware;

public sealed class JobLeaseMiddleware(ILeaseProvider leaseProvider, ILogger<JobLeaseMiddleware> logger)
    : IJobMiddleware
{
    private static readonly TimeSpan s_defaultLeaseDuration = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(JobContext context, CancellationToken cancellationToken, JobExecutionDelegate next)
    {
        var leaseName = $"job:{context.Name}";

        var lease = await leaseProvider.TryAcquireAsync(leaseName, s_defaultLeaseDuration, cancellationToken);

        try
        {
            if (lease is null)
            {
                logger.LogInformation("Skipping {JobName} because lease could not be acquired", context.Name);

                return;
            }

            logger.LogInformation("Lease acquired for {JobName}", context.Name);

            await next(context, cancellationToken);
        }
        finally
        {
            if (lease != null)
                await lease.DisposeAsync();
        }
    }
}
