namespace Infrastructure.Scheduler;

public interface IJobExecutor
{
    Task ExecuteAsync(ICronJob job, JobSettings settings, CancellationToken cancellationToken);
}
