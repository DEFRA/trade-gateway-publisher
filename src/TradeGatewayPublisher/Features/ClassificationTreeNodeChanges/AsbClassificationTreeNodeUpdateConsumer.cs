using Infrastructure;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Options;
using Trade.Gateway.Api.Contract.Events;

namespace TradeGatewayPublisher.Features.ClassificationTreeNodeChanges
{
    public partial class AsbClassificationTreeNodeUpdateConsumer(
        IAsbPublisher awsPublisher,
        IOptions<TracesServiceBusOptions> options,
        ILogger<AsbClassificationTreeNodeUpdateConsumer> logger
    ) : IMessageConsumer
    {
        public async Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
        {
            var eventId = context.Body.FromJson<EventEnvelope<object>>()?.EventId;

            LogPublishingTreeNode(eventId, options.Value.ClassificationTreeNode.TopicName);

            await awsPublisher.PublishAsync(
                options.Value.ClassificationTreeNode.TopicName,
                messageId: context.MessageId,
                context.Headers.ToDictionary(header => header.Key, header => header.Value.StringValue),
                context.Body,
                cancellationToken: cancellationToken
            );

            LogPublishedTreeNode(eventId, options.Value.ClassificationTreeNode.TopicName);
        }

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Publishing Classification Tree Node event {EventId} to ASB topic {AsbTopic}"
        )]
        private partial void LogPublishingTreeNode(Guid? eventId, string asbTopic);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Published Classification Tree Node event {EventId} to ASB topic {AsbTopic}"
        )]
        private partial void LogPublishedTreeNode(Guid? eventId, string asbTopic);
    }
}
