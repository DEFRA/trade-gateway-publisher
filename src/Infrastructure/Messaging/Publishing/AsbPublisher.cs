using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging.Extensions;
using Microsoft.Extensions.Azure;

namespace Infrastructure.Messaging.Publishing;

public class AsbPublisher(
    IAzureClientFactory<ServiceBusSender> serviceBusSenderFactory,
    IEnumerable<IPublishMiddleware>? middlewares = null
) : IAsbPublisher
{
    private readonly IReadOnlyList<IPublishMiddleware> _middlewares = middlewares?.ToList() ?? [];

    public async Task PublishAsync(
        string topicName,
        string messageId,
        Dictionary<string, string> messageHeaders,
        string messageBody,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(topicName))
            throw new ArgumentException("Topic name is required.", nameof(topicName));

        if (string.IsNullOrWhiteSpace(messageBody))
            throw new ArgumentException("Message body is required.", nameof(messageBody));

        var publishContext = new PublishContext
        {
            Headers = messageHeaders,
            MessageBody = messageBody,
            TopicName = topicName,
        };

        var sender = serviceBusSenderFactory.CreateClient(topicName);

        Task PublishCore()
        {
            var request = publishContext.ToServiceBusMessage();
            request.MessageId = messageId;
            return sender.SendMessageAsync(request, cancellationToken);
        }

        var pipeline = PublishCore;

        foreach (var middleware in _middlewares.Reverse())
        {
            var next = pipeline;

            pipeline = () => middleware.InvokeAsync(publishContext, next, cancellationToken);
        }

        await pipeline();
    }
}
