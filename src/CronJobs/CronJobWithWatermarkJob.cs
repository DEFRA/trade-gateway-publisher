using CronJobs.Watermark;

namespace CronJobs;

public abstract class CronJobWithWatermarkJob(IJobWatermarkStore watermarkStore) : ICronJob
{
    public abstract string Name { get; }

    public async Task ExecuteAsync(CronJobWithWatermarkJob.JobContext context, CancellationToken cancellationToken)
    {
        var watermark = await watermarkStore.GetAsync(Name, cancellationToken) ?? DateTimeOffset.UtcNow.AddMinutes(-5);

        await DoExecuteAsync(
            new WatermarkJobContext(context.JobId, context.Name, watermark, DateTimeOffset.UtcNow),
            cancellationToken
        );

        await watermarkStore.SetAsync(Name, DateTimeOffset.UtcNow, cancellationToken);
    }

    public abstract Task DoExecuteAsync(WatermarkJobContext context, CancellationToken cancellationToken);

    public record WatermarkJobContext(string JobId, string Name, DateTimeOffset Watermark, DateTimeOffset Now)
        : JobContext(JobId, Name);

    public record JobContext(string JobId, string Name);
}
