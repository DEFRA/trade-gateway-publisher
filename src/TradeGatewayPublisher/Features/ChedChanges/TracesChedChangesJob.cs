using System.Text.Json;
using Infrastructure;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Scheduler;
using Microsoft.Extensions.Options;
using Refit;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Certificate;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.ChedChanges;

public sealed class TracesChedChangesJob(
    ITracesGatewayChedClient tracesGateway,
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
                    watermark.Watermark,
                    watermark.Now,
                    pageSize,
                    offset,
                    cancellationToken
                );

                await updatesResponse.EnsureSuccessfulAsync();

                var responseData =
                    updatesResponse.Content?.Items ?? Enumerable.Empty<DefraUNVTDCHEDSummaryProfileItem>();
                hasMoreUpdates = updatesResponse.Content is { HasMore: true };

                var topicArn = options.Value.ChedInternalTopicArn;

                foreach (var update in responseData)
                {
                    // Publish each update to SNS - this could prob become a batch
                    await snsPublisher.PublishAsync(
                        topicArn,
                        update.ToJson(),
                        cancellationToken: cancellationToken,
                        duplicationId: update.Id
                    );
                    logger.LogInformation("Published CHED {Id} to {Topic}", update.Id, topicArn);
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
