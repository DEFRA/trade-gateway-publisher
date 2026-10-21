using Infrastructure;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Options;
using Trade.Gateway.Api.Contract.Events;

namespace TradeGatewayPublisher.Features.ClassificationTreeNodeChanges
{
    public class AsbClassificationTreeNodeUpdateConsumer(
        IAsbPublisher awsPublisher,
        IOptions<TracesServiceBusOptions> options,
        ILogger<AsbClassificationTreeNodeUpdateConsumer> logger
    ) : IMessageConsumer
    {
        public async Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
        {
            var eventId = context.Body.FromJson<EventEnvelope<object>>()?.EventId;

            logger.LogInformation(
                "Publishing Classification Tree Node  event {Id} to ASB topic {Topic}",
                eventId,
                options.Value.ClassificationTreeNode.TopicName
            );

            await awsPublisher.PublishAsync(
                options.Value.ClassificationTreeNode.TopicName,
                messageId: context.MessageId,
                context.Headers.ToDictionary(header => header.Key, header => header.Value.StringValue),
                context.Body,
                cancellationToken: cancellationToken
            );

            logger.LogInformation(
                "Published Classification Tree Node  event {Id} to ASB topic {Topic}",
                eventId,
                options.Value.ClassificationTreeNode.TopicName
            );
        }
    }
}
