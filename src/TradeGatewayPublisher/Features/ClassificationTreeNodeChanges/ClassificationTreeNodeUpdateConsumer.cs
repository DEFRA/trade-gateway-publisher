using Infrastructure;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Options;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Events;
using Trade.Gateway.Api.Contract.ReferenceData;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.ClassificationTreeNodeChanges
{
    public partial class ClassificationTreeNodeUpdateConsumer(
        ITracesGatewayClient tracesGateway,
        ISnsPublisher snsPublisher,
        IOptions<TracesUpdatePublisherOptions> options,
        ILogger<ClassificationTreeNodeUpdateConsumer> logger
    ) : IMessageConsumer
    {
        public async Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
        {
            var message = context.Body.FromJson<ClassificationTreeNode>();

            var treeId = context.Headers[ClassificationTreeMessageHeaderKeys.TreeId].StringValue;
            var duplicationId = context.Headers[ClassificationTreeMessageHeaderKeys.DuplicationId].StringValue;

            var apiResponse = await tracesGateway.GetClassificationTreeNodeDetail(
                treeId,
                message!.NodeId,
                cancellationToken
            );

            await apiResponse.EnsureSuccessStatusCodeAsync();
            var treeNodeDetailResponse = apiResponse.Content!;

            await PublishAsync(treeNodeDetailResponse, duplicationId, context, cancellationToken);
        }

        private async Task PublishAsync(
            DefraUNVTDProfileClassificationTreeNodeDetailResponse treeNodeDetail,
            string duplicationId,
            MessageContext context,
            CancellationToken cancellationToken
        )
        {
            var @event = treeNodeDetail.ToEventEnvelope(context.GetTraceId());

            await snsPublisher.PublishAsync(
                options.Value.ClassificationTreeNodeTopicArn,
                @event.ToJson(),
                duplicationId: duplicationId,
                cancellationToken: cancellationToken
            );

            LogPublishedTreeNode(
                @event.EventId,
                treeNodeDetail.NodeId,
                treeNodeDetail.TreeId,
                options.Value.ClassificationTreeNodeTopicArn
            );
        }

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Published event {EventId} for classification tree node {NodeId} from tree {TreeId} to SNS ARN {Topic}"
        )]
        private partial void LogPublishedTreeNode(Guid? eventId, string? nodeId, string? treeId, string topic);
    }
}
