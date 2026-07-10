using System.Text.Json;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Infrastructure.TracesGateway;
using Microsoft.Extensions.Options;
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

            var response = await tracesGateway.GetChedCertification(message!.Id, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Placeholder deduplication id — see "Message Deduplication" in README.md
            await snsPublisher.PublishAsync(
                options.Value.ChedTopicArn,
                await response.Content.ReadAsStringAsync(cancellationToken),
                duplicationId: Guid.NewGuid().ToString("N"),
                cancellationToken: cancellationToken
            );
            logger.LogInformation("Published CHED {Id} to {Topic}", message.Id, options.Value.ChedTopicArn);
        }
    }
}
