using Infrastructure.Scheduler;

namespace TradeGatewayPublisher.Jobs;

public sealed class ExampleJob(ILogger<ExampleJob> logger) : ICronJob
{
    public string Name => "ExampleJob";

    public async Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Doing work...");
        await Task.Delay(30000, cancellationToken);

        logger.LogInformation("Work complete");
    }
}
