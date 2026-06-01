using Infrastructure.Messaging.Publishing;
using Infrastructure.Scheduler;
using Infrastructure.TracesGateway;
using Microsoft.Extensions.Options;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.IntraChanges;

public sealed class TracesIntraChangesJob(
    ITracesGateway tracesGateway,
    ISnsPublisher snsPublisher,
    IOptions<TracesUpdatePublisherOptions> options
) : ICronJob
{
    public string Name => "TracesIntraChangesJob";

    public async Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        var watermark = context.GetRequired<WatermarkContext>();
        var hasMoreUpdates = true;
        var offset = 0;
        var pageSize = 100;

        do
        {
            var updatesResponse = await tracesGateway.FindIntraUpdates(
                watermark.Watermark.UtcDateTime,
                watermark.Now.UtcDateTime,
                pageSize,
                offset,
                cancellationToken
            );
            hasMoreUpdates = updatesResponse.Data.Any();

            foreach (var update in updatesResponse.Data)
            {
                // Publish each update to SNS - this could prob become a batch
                await snsPublisher.PublishAsync(
                    options.Value.IntraInternalTopicArn,
                    update,
                    cancellationToken: cancellationToken
                );
            }
            if (hasMoreUpdates)
                offset += pageSize;
        } while (hasMoreUpdates);
    }
}
