namespace Infrastructure.Messaging.Publishing;

public interface ISnsPublisher
{
    Task PublishAsync(
        string topicArn,
        string messageBody,
        Dictionary<string, string>? headers = null,
        string? subject = null,
        CancellationToken cancellationToken = default
    );
}
