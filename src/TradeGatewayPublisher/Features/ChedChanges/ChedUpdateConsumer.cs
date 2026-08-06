using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Certificate;
using Trade.Gateway.Api.Contract.Events;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.ChedChanges
{
    public class ChedUpdateConsumer(
        ITracesGatewayChedClient tracesGateway,
        ISnsPublisher snsPublisher,
        IOptions<TracesUpdatePublisherOptions> options,
        ILogger<ChedUpdateConsumer> logger
    ) : IMessageConsumer
    {
        public async Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
        {
            var message = JsonSerializer.Deserialize<DefraUNVTDCHEDSummaryProfileItem>(context.Body);

            var apiResponse = await tracesGateway.GetChedCertification(message!.Id, cancellationToken);
            var certificate = apiResponse.Content;
            var @event = certificate!.ToEventEnvelope(context.GetTraceId());

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
