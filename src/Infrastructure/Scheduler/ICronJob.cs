namespace Infrastructure.Scheduler;

public interface ICronJob
{
    string Name { get; }

    Task ExecuteAsync(JobContext context, CancellationToken cancellationToken);
}
