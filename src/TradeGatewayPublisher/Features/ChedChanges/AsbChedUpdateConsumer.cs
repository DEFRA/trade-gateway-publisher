using System.Text.Json;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Options;
using Trade.Gateway.Api.Contract.Events;

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
            // only need the envelope here
            var eventId = JsonSerializer.Deserialize<EventEnvelope<object>>(context.Body)?.EventId;

            logger.LogInformation(
                "Publishing CHED event {Id} to ASB topic {Topic}",
                eventId,
                options.Value.Ched.TopicName
            );

            // Placeholder deduplication id — see "Message Deduplication" in README.md
            await asbPublisher.PublishAsync(
                options.Value.Ched.TopicName,
                messageId: context.MessageId,
                context.Headers.ToDictionary(header => header.Key, header => header.Value.StringValue),
                context.Body,
                cancellationToken: cancellationToken
            );

            logger.LogInformation(
                "Published CHED event {Id} to ASB topic {Topic}",
                eventId,
                options.Value.Ched.TopicName
            );
        }
    }
}
