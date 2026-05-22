namespace CronJobs;

public delegate Task JobExecutionDelegate(
    CronJobWithWatermarkJob.JobContext context,
    CancellationToken cancellationToken
);
