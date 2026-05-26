using Infrastructure.Scheduler;

namespace TradeGatewayPublisher.Jobs.Middleware;

public sealed class JobMetricsMiddleware(ILogger<JobMetricsMiddleware> logger) : IJobMiddleware
{
    public async Task InvokeAsync(JobContext context, CancellationToken cancellationToken, JobExecutionDelegate next)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await next(context, cancellationToken);

            logger.LogInformation(
                "Metrics: {JobName} succeeded in {ElapsedMs}ms",
                context.Name,
                sw.ElapsedMilliseconds
            );
        }
        catch
        {
            ////logger.LogWarning("Metrics: {JobName} failed after {ElapsedMs}ms", context.Name, sw.ElapsedMilliseconds);

            ////throw;
        }
        finally
        {
            sw.Stop();
        }
    }
}
