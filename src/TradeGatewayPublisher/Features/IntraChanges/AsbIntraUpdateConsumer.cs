using Infrastructure.Messaging;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Options;

namespace TradeGatewayPublisher.Features.IntraChanges
{
    public class AsbIntraUpdateConsumer(
        IAsbPublisher awsPublisher,
        IOptions<TracesServiceBusOptions> options,
        ILogger<AsbIntraUpdateConsumer> logger
    ) : IMessageConsumer
    {
        public async Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
        {
            // Placeholder deduplication id — see "Message Deduplication" in README.md
            await awsPublisher.PublishAsync(
                options.Value.Intra.TopicName,
                messageId: context.MessageId,
                context.Headers.ToDictionary(header => header.Key, header => header.Value.StringValue),
                context.Body,
                cancellationToken: cancellationToken
            );

            logger.LogInformation(
                "Published INTRA message id {Id} to {Queue}",
                context.MessageId,
                options.Value.Intra.TopicName
            );
        }
    }
}
