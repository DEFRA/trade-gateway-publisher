using Amazon.SQS;
using Amazon.SQS.Model;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Testing;

namespace TradeGatewayPublisher.IntegrationTests.IntraChanges;

[Trait("Category", "IntegrationTest")]
[Collection(NonParallelCollection.Name)]
public class IntraPollingSnsIntegrationTest(IntegrationTestFixture fixture, ITestOutputHelper testOutputHelper)
    : IAsyncLifetime
{
    private readonly string IntraId = $"intra-test-1-aws-{Random.Shared.Next(1, 100000)}";

    private IDisposable? _logCapture;

    [Fact]
    public async Task IntraPollingJobPolls_AndThenPublishesToSns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await WireMockStubber.StubAsync(fixture.Factory.WireMockBaseUrl, IntraId, cancellationToken);

        var received = await WaitHelper.WaitUntilAsync(
            () =>
                QueueContainsExpectedAsync(
                        fixture.AmazonSqs,
                        fixture.QueueUrl,
                        IntraId,
                        testOutputHelper,
                        cancellationToken
                    )
                    .GetAwaiter()
                    .GetResult(),
            TimeSpan.FromSeconds(120),
            TimeSpan.FromMilliseconds(500),
            cancellationToken
        );

        received.Should().BeTrue("The Intra polling job should have polled Traces and published to SNS within 120s");
    }

    private static async Task<bool> QueueContainsExpectedAsync(
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

    public async ValueTask InitializeAsync()
    {
        _logCapture = TestOutputHelperSink.Capture(testOutputHelper);

        await WireMockStubber.ResetAsync(fixture.Factory.WireMockBaseUrl);
        await fixture.DeleteDatabaseAsync();
        await fixture.AmazonSqs.PurgeQueueAsync(fixture.QueueUrl);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async ValueTask DisposeAsync()
    {
        _logCapture?.Dispose();
    }
}
