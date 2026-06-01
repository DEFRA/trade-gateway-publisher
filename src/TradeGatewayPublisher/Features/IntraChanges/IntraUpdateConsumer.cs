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
        ILogger<IntraUpdateConsumer> logger,
        ISnsPublisher snsPublisher,
        IOptions<TracesUpdatePublisherOptions> options
    ) : IMessageConsumer
    {
        public async Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Consumed Message: {MessageId}", context.MessageId);

            var message = JsonSerializer.Deserialize<FindIntraUpdatesResponseRecord>(context.Body);

            var response = await tracesGateway.GetIntraCertification(message!.Id, cancellationToken);
            response.EnsureSuccessStatusCode();

            await snsPublisher.PublishAsync(
                options.Value.IntraTopicArn,
                await response.Content.ReadAsStringAsync(cancellationToken),
                cancellationToken: cancellationToken
            );

            logger.LogInformation("Published Message: {MessageId}", context.MessageId);
        }
    }
}
