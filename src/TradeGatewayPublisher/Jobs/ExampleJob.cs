using CronJobs;
using CronJobs.Watermark;

namespace TradeGatewayPublisher.Jobs;

public sealed class ExampleJob(IJobWatermarkStore watermarkStore, ILogger<ExampleJob> logger)
    : CronJobWithWatermarkJob(watermarkStore)
{
    public override string Name => "ExampleJob";

    public override async Task DoExecuteAsync(WatermarkJobContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Doing work...");

        await Task.Delay(1000, cancellationToken);

        logger.LogInformation("Work complete");
    }
}
