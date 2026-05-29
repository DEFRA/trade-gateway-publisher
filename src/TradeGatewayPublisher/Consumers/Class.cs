using Infrastructure.Messaging.Consuming;

namespace TradeGatewayPublisher.Consumers
{
    public class IntraUpdateConsumer(ILogger<IntraUpdateConsumer> logger) : IMessageConsumer
    {
        public Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Consumed Message: {Message}", context.Body);
            return Task.CompletedTask;
        }
    }
}
