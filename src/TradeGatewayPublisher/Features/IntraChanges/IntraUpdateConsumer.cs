using System.Text.Json;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Options;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Certificate;
using Trade.Gateway.Api.Contract.Events;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.IntraChanges;

public class IntraUpdateConsumer(
    ITracesGatewayIntraClient tracesGateway,
    ISnsPublisher snsPublisher,
    IOptions<TracesUpdatePublisherOptions> options,
    ILogger<IntraUpdateConsumer> logger
) : IMessageConsumer
{
    public async Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
    {
        var message = JsonSerializer.Deserialize<DefraUNVTDINTRASummaryProfileItem>(context.Body);

        var apiResponse = await tracesGateway.GetIntraCertification(message!.Id, cancellationToken);
        await apiResponse.EnsureSuccessfulAsync();
        var certificate = apiResponse.Content;
        var @event = certificate!.ToEventEnvelope(context.GetTraceId());

        // Placeholder deduplication id — see "Message Deduplication" in README.md
        await snsPublisher.PublishAsync(
            options.Value.IntraTopicArn,
            JsonSerializer.Serialize(@event),
            duplicationId: Guid.NewGuid().ToString("N"),
            cancellationToken: cancellationToken
        );
        logger.LogInformation("Published INTRA {Id} to {Topic}", message.Id, options.Value.IntraTopicArn);
    }
}
