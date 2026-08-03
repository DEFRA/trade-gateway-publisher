using System.Text.Json;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Infrastructure.TracesGateway;
using Microsoft.Extensions.Options;
using Trade.Gateway.Api.Contract.Events;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.ChedChanges
{
    public class ChedUpdateConsumer(
        ITracesGateway tracesGateway,
        ISnsPublisher snsPublisher,
        IOptions<TracesUpdatePublisherOptions> options,
        ILogger<ChedUpdateConsumer> logger
    ) : IMessageConsumer
    {
        public async Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
        {
            var message = JsonSerializer.Deserialize<FindChedUpdatesResponseRecord>(context.Body);

            var certificate = await tracesGateway.GetChedCertification(message!.Id, cancellationToken);

            var @event = certificate.ToEventEnvelope(context.GetTraceId());

            // Placeholder deduplication id — see "Message Deduplication" in README.md
            await snsPublisher.PublishAsync(
                options.Value.ChedTopicArn,
                JsonSerializer.Serialize(@event),
                duplicationId: Guid.NewGuid().ToString("N"),
                cancellationToken: cancellationToken
            );
            logger.LogInformation("Published CHED {Id} to {Topic}", message.Id, options.Value.ChedTopicArn);
        }
    }
}
