using Infrastructure.Messaging;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Options;

namespace TradeGatewayPublisher.Features.ChedChanges
{
    public class AsbChedUpdateConsumer(
        IAsbPublisher asbPublisher,
        IOptions<TracesServiceBusOptions> options,
        ILogger<AsbChedUpdateConsumer> logger
    ) : IMessageConsumer
    {
        public async Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
        {
            // Placeholder deduplication id — see "Message Deduplication" in README.md
            await asbPublisher.PublishAsync(
                options.Value.Ched.QueueName,
                messageId: context.MessageId,
                context.Headers.ToDictionary(header => header.Key, header => header.Value.StringValue),
                context.Body,
                cancellationToken: cancellationToken
            );

            logger.LogInformation(
                "Published CHED message id {Id} to {Queue}",
                context.MessageId,
                options.Value.Ched.QueueName
            );
        }
    }
}
