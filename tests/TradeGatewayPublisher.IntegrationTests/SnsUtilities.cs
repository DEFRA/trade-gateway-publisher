using Amazon.SQS;
using Amazon.SQS.Model;

namespace TradeGatewayPublisher.IntegrationTests
{
    public static class SnsUtilities
    {
        public static async Task<bool> SnsQueueContainsExpectedAsync(
            IAmazonSQS sqs,
            string queueUrl,
            string expectedId,
            ITestOutputHelper testOutputHelper,
            CancellationToken cancellationToken
        )
        {
            var response = await sqs.ReceiveMessageAsync(
                new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 2,
                    MessageAttributeNames = ["All"],
                },
                cancellationToken
            );

            if (response?.Messages == null || response.Messages.Count == 0)
                return false;

            foreach (var message in response.Messages)
            {
                testOutputHelper.WriteLine($"Deleting message {message.Body}");
                await sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);

                if (message.Body.Contains(expectedId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
