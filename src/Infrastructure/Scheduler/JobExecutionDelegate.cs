namespace Infrastructure.Scheduler;

public delegate Task JobExecutionDelegate(JobContext context, CancellationToken cancellationToken);
