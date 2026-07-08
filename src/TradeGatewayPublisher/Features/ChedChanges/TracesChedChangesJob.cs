using Infrastructure.Messaging.Publishing;
using Infrastructure.Scheduler;
using Infrastructure.TracesGateway;
using Microsoft.Extensions.Options;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.ChedChanges;

public sealed class TracesChedChangesJob(
    ITracesGateway tracesGateway,
    ISnsPublisher snsPublisher,
    IOptions<TracesUpdatePublisherOptions> options
) : ICronJob
{
    public string Name => "TracesChedChangesJob";

    public async Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        var watermark = context.GetRequired<WatermarkContext>();
        var hasMoreUpdates = true;
        var offset = 0;
        var pageSize = 100;

        do
        {
            var updatesResponse = await tracesGateway.FindChedUpdates(
                watermark.Watermark.UtcDateTime,
                watermark.Now.UtcDateTime,
                pageSize,
                offset,
                cancellationToken
            );

            var responseData = updatesResponse?.Items ?? Enumerable.Empty<FindChedUpdatesResponseRecord>();
            hasMoreUpdates = responseData.Count() == pageSize;

            foreach (var update in responseData)
            {
                // Publish each update to SNS - this could prob become a batch
                await snsPublisher.PublishAsync(
                    options.Value.ChedInternalTopicArn,
                    update,
                    cancellationToken: cancellationToken
                );
            }
            if (hasMoreUpdates)
                offset += pageSize;
        } while (hasMoreUpdates);
    }
}
