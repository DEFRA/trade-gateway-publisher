using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Infrastructure.Messaging.Publishing;

public class SnsPublisher(
    IAmazonSimpleNotificationService snsClient,
    IEnumerable<IPublishMiddleware>? middlewares = null
) : ISnsPublisher
{
    private readonly IReadOnlyList<IPublishMiddleware> _middlewares = middlewares?.ToList() ?? [];

    public async Task PublishAsync(
        string topicArn,
        string messageBody,
        Dictionary<string, string>? headers = null,
        string? subject = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(topicArn))
            throw new ArgumentException("Topic ARN is required.", nameof(topicArn));

        if (string.IsNullOrWhiteSpace(messageBody))
            throw new ArgumentException("Message body is required.", nameof(messageBody));

        var context = new PublishContext
        {
            TopicArn = topicArn,
            MessageBody = messageBody,
            Subject = subject,
        };

        if (headers != null)
        {
            foreach (var header in headers)
            {
                context.Headers[header.Key] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = header.Value,
                };
            }
        }

        Task PublishCore()
        {
            var request = context.ToPublishRequest();
            return snsClient.PublishAsync(request, cancellationToken);
        }

        var pipeline = PublishCore;

        foreach (var middleware in _middlewares.Reverse())
        {
            var next = pipeline;

            pipeline = () => middleware.InvokeAsync(context, next, cancellationToken);
        }

        await pipeline();
    }
}
