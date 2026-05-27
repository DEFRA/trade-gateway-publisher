using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging.Consuming;

public class SqsConsumerBackgroundService(
    string queueUrl,
    IAmazonSQS sqsClient,
    IMessageConsumer consumer,
    ILogger<SqsConsumerBackgroundService> logger,
    IEnumerable<IConsumeMiddleware>? middlewares = null
) : BackgroundService
{
    private readonly IReadOnlyList<IConsumeMiddleware> _middlewares = middlewares?.ToList() ?? [];

    public string QueueUrl { get; } = queueUrl;

    public int MaxMessages { get; init; } = 10;

    public int WaitTimeSeconds { get; init; } = 20;

    public int VisibilityTimeoutSeconds { get; init; } = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting SQS consumer for queue {QueueUrl}", QueueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqsClient.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = QueueUrl,
                        MaxNumberOfMessages = MaxMessages,
                        WaitTimeSeconds = WaitTimeSeconds,
                        VisibilityTimeout = VisibilityTimeoutSeconds,
                        MessageAttributeNames = ["All"],
                    },
                    stoppingToken
                );

                if (response.Messages.Count == 0)
                    continue;

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error while polling SQS");
            }
        }

        logger.LogInformation("Stopping SQS consumer for queue {QueueUrl}", QueueUrl);
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var context = new MessageContext { Message = message, QueueUrl = QueueUrl };

        Task ConsumerCore()
        {
            return consumer.ConsumeAsync(context, cancellationToken);
        }

        var pipeline = ConsumerCore;

        foreach (var middleware in _middlewares.Reverse())
        {
            var next = pipeline;

            pipeline = () => middleware.InvokeAsync(context, next, cancellationToken);
        }

        try
        {
            await pipeline();

            await sqsClient.DeleteMessageAsync(QueueUrl, message.ReceiptHandle, cancellationToken);

            logger.LogInformation("Processed SQS message {MessageId}", message.MessageId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed processing SQS message {MessageId}", message.MessageId);

            // Message remains in queue and becomes visible again
            // after visibility timeout expires.
        }
    }
}
