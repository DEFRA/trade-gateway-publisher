using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Infrastructure.Messaging.Extensions;

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
        string? duplicationId = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(topicArn))
            throw new ArgumentException("Topic ARN is required.", nameof(topicArn));

        if (string.IsNullOrWhiteSpace(messageBody))
            throw new ArgumentException("Message body is required.", nameof(messageBody));

        var context = new PublishContext
        {
            TopicName = topicArn.ToTopicNameFromTopicArn(),
            MessageBody = messageBody,
            Subject = subject,
            Headers = headers ?? new Dictionary<string, string>(),
        };

        Task PublishCore()
        {
            var request = context.ToSnsPublishRequest(topicArn);
            request.MessageDeduplicationId = duplicationId;
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

    public Task PublishAsync<T>(
        string topicArn,
        T message,
        Dictionary<string, string>? headers = null,
        string? subject = null,
        CancellationToken cancellationToken = default
    )
        where T : IMessage
    {
        if (string.IsNullOrWhiteSpace(topicArn))
            throw new ArgumentException("Topic ARN is required.", nameof(topicArn));

        if (message is null)
            throw new ArgumentException("Message is required.", nameof(message));

        return PublishAsync(topicArn, message.ToJson(), headers, subject, message.DuplicationId, cancellationToken);
    }
}
