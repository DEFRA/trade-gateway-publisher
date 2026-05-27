using Infrastructure.Scheduler;
using Infrastructure.Scheduler.Metrics;

namespace TradeGatewayPublisher.Jobs.Middleware;

public sealed class JobMetricsMiddleware(JobMetrics metrics) : IJobMiddleware
{
    public async Task InvokeAsync(JobContext context, CancellationToken cancellationToken, JobExecutionDelegate next)
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();

        try
        {
            metrics.Start(context.Name);

            await next(context, cancellationToken);
        }
        catch (Exception exception)
        {
            metrics.Faulted(context.Name, exception);

            throw;
        }
        finally
        {
            metrics.Complete(context.Name, TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds);
        }
    }
}
