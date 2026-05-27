namespace Infrastructure.Messaging.Consuming;

public interface IMessageConsumer
{
    Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default);
}
