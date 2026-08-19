using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Options;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Certificate;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.IntraChanges;

public sealed class TracesIntraChangesJob(
    ITracesGatewayIntraClient tracesGateway,
    ISnsPublisher snsPublisher,
    IOptions<TracesUpdatePublisherOptions> options,
    ILogger<TracesIntraChangesJob> logger
) : TracesChangesJobBase<DefraUNVTDINTRASummaryProfileItem>(snsPublisher, logger)
{
    public override string Name => "TracesIntraChangesJob";

    protected override string SourceTag => "INTRA";

    protected override string GetTopicArn() => options.Value.IntraInternalTopicArn;

    protected override string GetId(DefraUNVTDINTRASummaryProfileItem item) => item.Id;

    protected override async Task<TracesChangesPage<DefraUNVTDINTRASummaryProfileItem>> FetchPageAsync(
        DateTimeOffset watermark,
        DateTimeOffset now,
        int pageSize,
        int offset,
        CancellationToken cancellationToken
    )
    {
        var updatesResponse = await tracesGateway.FindIntraUpdates(watermark, now, pageSize, offset, cancellationToken);

        await updatesResponse.EnsureSuccessfulAsync();

        var items = updatesResponse.Content?.Items ?? Enumerable.Empty<DefraUNVTDINTRASummaryProfileItem>();
        var hasMore = updatesResponse.Content is { HasMore: true };

        return new TracesChangesPage<DefraUNVTDINTRASummaryProfileItem>(items, hasMore);
    }
}
