using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging.Publishing;

/// <summary>
/// Azure Service Bus Publisher that does nothing other than log.
/// This is necessary to prevent queue build up if the feature is disabled.
/// </summary>
public class NullAsbPublisher(ILogger<NullAsbPublisher> logger) : IAsbPublisher
{
    public async Task PublishAsync(
        string topicName,
        string messageId,
        Dictionary<string, string> messageHeaders,
        string messageBody,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Publishing to Azure Service Bus is disabled");
        await Task.CompletedTask;
    }
}
