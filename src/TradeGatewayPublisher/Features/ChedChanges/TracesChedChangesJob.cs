using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Options;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Certificate;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.ChedChanges;

public sealed class TracesChedChangesJob(
    ITracesGatewayChedClient tracesGateway,
    ISnsPublisher snsPublisher,
    IOptions<TracesUpdatePublisherOptions> options,
    ILogger<TracesChedChangesJob> logger
) : TracesChangesJobBase<DefraUNVTDCHEDSummaryProfileItem>(snsPublisher, logger)
{
    public override string Name => "TracesChedChangesJob";

    protected override string SourceTag => "CHED";

    protected override string GetTopicArn() => options.Value.ChedInternalTopicArn;

    protected override string GetId(DefraUNVTDCHEDSummaryProfileItem item) => item.Id;

    protected override async Task<TracesChangesPage<DefraUNVTDCHEDSummaryProfileItem>> FetchPageAsync(
        DateTimeOffset watermark,
        DateTimeOffset now,
        int pageSize,
        int offset,
        CancellationToken cancellationToken
    )
    {
        var updatesResponse = await tracesGateway.FindChedUpdates(watermark, now, pageSize, offset, cancellationToken);

        await updatesResponse.EnsureSuccessfulAsync();

        var items = updatesResponse.Content?.Items ?? Enumerable.Empty<DefraUNVTDCHEDSummaryProfileItem>();
        var hasMore = updatesResponse.Content is { HasMore: true };

        return new TracesChangesPage<DefraUNVTDCHEDSummaryProfileItem>(items, hasMore);
    }
}
