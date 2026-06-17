using System.Text.Json;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Scheduler;
using Infrastructure.TracesGateway;
using Microsoft.Extensions.Options;
using Refit;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.IntraChanges;

public sealed class TracesIntraChangesJob(
    ITracesGateway tracesGateway,
    ISnsPublisher snsPublisher,
    IOptions<TracesUpdatePublisherOptions> options,
    ILogger<TracesIntraChangesJob> logger
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
            try
            {
                var updatesResponse = await tracesGateway.FindIntraUpdates(
                    watermark.Watermark.UtcDateTime.ToUniversalTime(),
                    watermark.Now.UtcDateTime.ToUniversalTime(),
                    pageSize,
                    offset,
                    cancellationToken
                );

                var responseData = updatesResponse?.Data ?? Enumerable.Empty<FindIntraUpdatesResponseRecord>();
                hasMoreUpdates = responseData.Any();

                foreach (var update in responseData)
                {
                    // Publish each update to SNS - this could prob become a batch
                    await snsPublisher.PublishAsync(
                        options.Value.IntraInternalTopicArn,
                        update,
                        cancellationToken: cancellationToken
                    );
                }
            }
#pragma warning disable S2139
            catch (ValidationApiException e)
#pragma warning restore S2139
            {
                logger.LogWarning(e, "{Job} failed validation - {Data}", Name, JsonSerializer.Serialize(e.Content));
                throw;
            }

            if (hasMoreUpdates)
                offset += pageSize;
        } while (hasMoreUpdates);
    }
}
