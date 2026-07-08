using System.Text.Json;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Scheduler;
using Infrastructure.TracesGateway;
using Microsoft.Extensions.Options;
using Refit;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.ChedChanges;

public sealed class TracesChedChangesJob(
    ITracesGateway tracesGateway,
    ISnsPublisher snsPublisher,
    IOptions<TracesUpdatePublisherOptions> options,
    ILogger<TracesChedChangesJob> logger
) : ICronJob
{
    public string Name => "TracesChedChangesJob";

    public async Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        var watermark = context.GetRequired<WatermarkContext>();
        var hasMoreUpdates = true;
        var offset = 0;
        var pageSize = 100;
        int changesFoundCount = 0;

        do
        {
            try
            {
                var updatesResponse = await tracesGateway.FindChedUpdates(
                    watermark.Watermark.UtcDateTime.ToUniversalTime(),
                    watermark.Now.UtcDateTime.ToUniversalTime(),
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
                    changesFoundCount++;
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

        logger.LogInformation("{Job} completed. {Count} Changes found", Name, changesFoundCount);
    }
}
