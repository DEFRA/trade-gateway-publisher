using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AwesomeAssertions;
using Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Infrastructure.Tests.Resilience;

public class ResilientSnsClientTests
{
    private readonly ResilientSnsClientRetryHandler _handler = new(Substitute.For<ILogger<ResilientSnsClientTests>>());

    [Fact]
    public async Task PublishWithRetryAsync_WhenTransientEntryFailuresOccur_RetriesOnlyFailedEntries()
    {
        var entries = Enumerable
            .Range(5, 5)
            .Select(i => new PublishBatchRequestEntry { Id = $"mrn-{i}-gmr123", Message = "gmr-data" })
            .ToList();

        var request = new PublishBatchRequest { TopicArn = "topicArn", PublishBatchRequestEntries = entries };

        var publishCalls = new List<IReadOnlyCollection<PublishBatchRequestEntry>>();
        var responses = new Queue<PublishBatchResponse>([
            new PublishBatchResponse
            {
                Failed =
                [
                    new BatchResultErrorEntry
                    {
                        Id = entries[0].Id,
                        SenderFault = false,
                        Code = "Throttled",
                    },
                ],
            },
            new PublishBatchResponse(),
        ]);

        await _handler.PublishWithRetryAsync(
            (req, _) =>
            {
                publishCalls.Add(req.PublishBatchRequestEntries);
                return Task.FromResult(responses.Dequeue());
            },
            request,
            CancellationToken.None
        );

        publishCalls.Should().HaveCount(2);
        publishCalls[0].Should().HaveCount(entries.Count);
        publishCalls[1].Should().ContainSingle(entry => entry.Id == entries[0].Id);
    }

    [Fact]
    public async Task PublishWithRetryAsync_WhenSenderFaultOccurs_DoesNotRetry()
    {
        var entries = new List<PublishBatchRequestEntry>
        {
            new() { Id = "mrn123-gmr123", Message = "msg" },
        };

        var request = new PublishBatchRequest { TopicArn = "topicArn", PublishBatchRequestEntries = entries };

        var callCount = 0;

        await _handler.PublishWithRetryAsync(
            (req, _) =>
            {
                callCount++;

                return Task.FromResult(
                    new PublishBatchResponse
                    {
                        Failed =
                        [
                            new BatchResultErrorEntry { Id = req.PublishBatchRequestEntries[0].Id, SenderFault = true },
                        ],
                    }
                );
            },
            request,
            CancellationToken.None
        );

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishWithRetryAsync_WhenAwsThrowsTransientException_Retries()
    {
        var entries = new List<PublishBatchRequestEntry>
        {
            new() { Id = "id-1", Message = "msg" },
        };

        var request = new PublishBatchRequest { TopicArn = "topic", PublishBatchRequestEntries = entries };

        var transientException = new AmazonSimpleNotificationServiceException("Receiver error")
        {
            ErrorType = ErrorType.Receiver,
        };

        var attempts = 0;

        await _handler.PublishWithRetryAsync(
            (req, _) =>
            {
                attempts++;

                if (attempts == 1)
                {
                    throw transientException;
                }

                return Task.FromResult(new PublishBatchResponse());
            },
            request,
            CancellationToken.None
        );

        attempts.Should().Be(2);
    }
}
