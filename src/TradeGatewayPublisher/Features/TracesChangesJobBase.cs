using System.Text.Json;
using Infrastructure;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Scheduler;
using Refit;

namespace TradeGatewayPublisher.Features;

public readonly record struct TracesChangesPage<TItem>(IEnumerable<TItem> Items, bool HasMore);

public abstract class TracesChangesJobBase<TItem>(ISnsPublisher snsPublisher, ILogger logger) : ICronJob
{
    private const int PageSize = 100;

    public abstract string Name { get; }

    protected abstract string SourceTag { get; }

    protected abstract string GetTopicArn();

    protected abstract string GetId(TItem item);

    protected abstract Task<TracesChangesPage<TItem>> FetchPageAsync(
        DateTimeOffset watermark,
        DateTimeOffset now,
        int pageSize,
        int offset,
        CancellationToken cancellationToken
    );

    public async Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        var watermark = context.GetRequired<WatermarkContext>();
        var hasMoreUpdates = true;
        var offset = 0;
        var changesFoundCount = 0;

        do
        {
            try
            {
                var page = await FetchPageAsync(
                    watermark.Watermark,
                    watermark.Now,
                    PageSize,
                    offset,
                    cancellationToken
                );
                var topicArn = GetTopicArn();

                foreach (var update in page.Items)
                {
                    // Publish each update to SNS - this could prob become a batch
                    await snsPublisher.PublishAsync(
                        topicArn,
                        update.ToJson(),
                        cancellationToken: cancellationToken,
                        duplicationId: GetId(update)
                    );
                    logger.LogInformation("Published {Source} {Id} to {Topic}", SourceTag, GetId(update), topicArn);
                    changesFoundCount++;
                }

                hasMoreUpdates = page.HasMore;
            }
#pragma warning disable S2139
            catch (ValidationApiException e)
#pragma warning restore S2139
            {
                logger.LogWarning(e, "{Job} failed validation - {Data}", Name, JsonSerializer.Serialize(e.Content));
                throw;
            }

            if (hasMoreUpdates)
                offset += PageSize;
        } while (hasMoreUpdates);

        logger.LogInformation("{Job} completed. {Count} Changes found", Name, changesFoundCount);
    }
}
