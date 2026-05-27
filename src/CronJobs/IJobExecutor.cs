namespace CronJobs;

public interface IJobExecutor
{
    Task ExecuteAsync(ICronJob job, JobSettings settings, CancellationToken cancellationToken);
}
