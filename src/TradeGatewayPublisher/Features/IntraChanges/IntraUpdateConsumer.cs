using System.Text.Json;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Infrastructure.TracesGateway;
using Microsoft.Extensions.Options;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.IntraChanges
{
    public class IntraUpdateConsumer(
        ITracesGateway tracesGateway,
        ISnsPublisher snsPublisher,
        IOptions<TracesUpdatePublisherOptions> options,
        ILogger<IntraUpdateConsumer> logger
    ) : IMessageConsumer
    {
        public async Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
        {
            var message = JsonSerializer.Deserialize<FindIntraUpdatesResponseRecord>(context.Body);

            var response = await tracesGateway.GetIntraCertification(message!.Id, cancellationToken);
            response.EnsureSuccessStatusCode();

            await snsPublisher.PublishAsync(
                options.Value.IntraTopicArn,
                await response.Content.ReadAsStringAsync(cancellationToken),
                duplicationId: Guid.NewGuid().ToString("N"),
                cancellationToken: cancellationToken
            );
            logger.LogInformation("Published INTRA {Id} to {Topic}", message.Id, options.Value.IntraTopicArn);
        }
    }
}
