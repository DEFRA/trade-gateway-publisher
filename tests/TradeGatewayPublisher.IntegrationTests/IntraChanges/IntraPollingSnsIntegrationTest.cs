using Amazon.SQS;
using Amazon.SQS.Model;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Testing;

namespace TradeGatewayPublisher.IntegrationTests.IntraChanges;

[Trait("Category", "IntegrationTest")]
public class IntraPollingSnsIntegrationTest : IAsyncLifetime
{
    private const string TestQueueName = "trade_gateway_publisher_intra_updates_test.fifo";
    private const string IntraId = "intra-test-1";

    private IntegrationTestWebApplicationFactory _factory = null!;
    private IAmazonSQS _sqs = null!;
    private string _queueUrl = null!;

    [Fact]
    public async Task IntraPollingJobPolls_AndThenPublishesToSns()
    {
        var cancellationToken = CancellationToken.None;

        await WireMockStubber.StubAsync(_factory.WireMockBaseUrl, IntraId, cancellationToken);

        var received = await WaitHelper.WaitUntilAsync(
            () => QueueContainsExpectedAsync(_sqs, _queueUrl, IntraId, cancellationToken).GetAwaiter().GetResult(),
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

        if (response.Messages.Count == 0)
            return false;

        foreach (var message in response.Messages)
        {
            await sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);

            if (message.Body.Contains(expectedId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public async ValueTask InitializeAsync()
    {
        _factory = new IntegrationTestWebApplicationFactory();
        _ = _factory.CreateClient();

        _sqs = _factory.Services.GetRequiredService<IAmazonSQS>();
        _queueUrl = (await _sqs.GetQueueUrlAsync(TestQueueName)).QueueUrl;

        await _sqs.PurgeQueueAsync(_queueUrl);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async ValueTask DisposeAsync()
    {
        await WireMockStubber.ResetAsync(_factory.WireMockBaseUrl);
        await _factory.DisposeAsync();
    }
}
