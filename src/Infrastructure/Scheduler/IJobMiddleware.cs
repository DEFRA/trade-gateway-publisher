namespace Infrastructure.Scheduler;

public interface IJobMiddleware
{
    Task InvokeAsync(JobContext context, CancellationToken cancellationToken, JobExecutionDelegate next);
}
