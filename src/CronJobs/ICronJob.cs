namespace CronJobs;

public interface ICronJob
{
    string Name { get; }

    Task ExecuteAsync(CronJobWithWatermarkJob.JobContext context, CancellationToken cancellationToken);
}
