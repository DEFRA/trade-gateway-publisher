using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Azure;

namespace Infrastructure.Messaging.Publishing;

public class AsbPublisher(
    IAzureClientFactory<ServiceBusSender> serviceBusSenderFactory,
    IEnumerable<IPublishMiddleware>? middlewares = null
) : IAsbPublisher
{
    private readonly IReadOnlyList<IPublishMiddleware> _middlewares = middlewares?.ToList() ?? [];

    public async Task PublishAsync(
        string queueName,
        string messageId,
        Dictionary<string, string> messageHeaders,
        string messageBody,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(queueName))
            throw new ArgumentException("Queue name is required.", nameof(queueName));

        if (string.IsNullOrWhiteSpace(messageBody))
            throw new ArgumentException("Message body is required.", nameof(messageBody));

        var asbPublishContext = new AsbPublishContext
        {
            Headers = messageHeaders,
            MessageBody = messageBody,
            QueueName = queueName,
        };

        var sender = serviceBusSenderFactory.CreateClient(queueName);

        Task PublishCore()
        {
            var request = asbPublishContext.ToServiceBusMessage();
            request.MessageId = messageId;
            return sender.SendMessageAsync(request, cancellationToken);
        }

        var pipeline = PublishCore;

        foreach (var middleware in _middlewares.Reverse())
        {
            var next = pipeline;

            pipeline = () => middleware.InvokeAsync(asbPublishContext, next, cancellationToken);
        }

        await pipeline();
    }
}
