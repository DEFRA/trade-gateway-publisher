using Amazon.Runtime;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Infrastructure.Resilience;

public class ResilientSnsClientRetryHandler(ILogger logger)
{
    private readonly AsyncRetryPolicy _retryPolicy = Policy
        .Handle<AmazonServiceException>(e => e.ErrorType is ErrorType.Receiver or ErrorType.Unknown)
        .Or<TransientBatchPublishException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: _ => TimeSpan.FromSeconds(1),
            onRetry: (exception, delay, retryAttempt, _) =>
            {
                if (exception is TransientBatchPublishException transientFailure)
                {
                    logger.LogWarning(
                        "Retrying SNS publish attempt {Attempt} for entries {Failures} because of transient failure codes: {Codes}",
                        retryAttempt,
                        string.Join(",", transientFailure.FailedEntryIds),
                        string.Join(",", transientFailure.FailureCodes)
                    );
                    return;
                }

                logger.LogWarning(
                    exception,
                    "Retrying SNS publish attempt {Attempt} after {Delay} due to transient AWS error",
                    retryAttempt,
                    delay
                );
            }
        );

    public Task<PublishBatchResponse> PublishWithRetryAsync(
        Func<PublishBatchRequest, CancellationToken, Task<PublishBatchResponse>> publish,
        PublishBatchRequest request,
        CancellationToken cancellationToken
    )
    {
        var pendingEntries = request.PublishBatchRequestEntries.ToList();

        return _retryPolicy.ExecuteAsync(
            async ct =>
            {
                var result = await publish(
                    new PublishBatchRequest
                    {
                        TopicArn = request.TopicArn,
                        PublishBatchRequestEntries = pendingEntries,
                    },
                    ct
                );

                if (result.Failed.Count == 0)
                {
                    return result;
                }

                var senderFaults = result.Failed.Where(f => f.SenderFault.HasValue && f.SenderFault.Value).ToList();
                var transientFaults = result.Failed.Where(f => f.SenderFault.HasValue && !f.SenderFault.Value).ToList();

                if (senderFaults.Count > 0)
                {
                    logger.LogError(
                        "SNS publish failed {Count} records due to our fault: {Failures}",
                        senderFaults.Count,
                        string.Join(",", senderFaults.Select(f => $"{f.Id}:{f.Code ?? "Unknown"}"))
                    );
                }

                pendingEntries = pendingEntries.Where(entry => transientFaults.Any(f => f.Id == entry.Id)).ToList();

                return pendingEntries.Count > 0 ? throw new TransientBatchPublishException(transientFaults) : result;
            },
            cancellationToken
        );
    }
}
