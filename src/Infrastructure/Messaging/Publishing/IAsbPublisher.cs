namespace Infrastructure.Messaging.Publishing;

public interface IAsbPublisher
{
    Task PublishAsync(
        string topicName,
        string messageId,
        Dictionary<string, string> messageHeaders,
        string messageBody,
        CancellationToken cancellationToken = default
    );
}
