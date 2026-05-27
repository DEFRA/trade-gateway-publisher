namespace CronJobs;

public interface IJobMiddleware
{
    Task InvokeAsync(
        CronJobWithWatermarkJob.JobContext context,
        CancellationToken cancellationToken,
        JobExecutionDelegate next
    );
}
