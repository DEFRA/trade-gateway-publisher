using System;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using AwesomeAssertions;
using Infrastructure.Watermark;
using Microsoft.Extensions.DependencyInjection;
using Testing;

namespace TradeGatewayPublisher.IntegrationTests.IntraChanges;

[Trait("Category", "IntegrationTest")]
[Collection(NonParallelCollection.Name)]
public class IntraPollingSnsIntegrationTest(ITestOutputHelper testOutputHelper) : IAsyncLifetime
{
    private IntegrationTestWebApplicationFactory _factory = null!;
    private IAmazonSQS _sqs = null!;
    private string _queueUrl = null!;
    private readonly string IntraId = $"intra-test-1-aws-{Random.Shared.Next(1, 10000)}";
    private HttpClient? _client;

    [Fact]
    public async Task IntraPollingJobPolls_AndThenPublishesToSns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await WireMockStubber.StubAsync(_factory.WireMockBaseUrl, IntraId, cancellationToken);

        var received = await WaitHelper.WaitUntilAsync(
            () =>
                QueueContainsExpectedAsync(_sqs, _queueUrl, IntraId, testOutputHelper, cancellationToken)
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
        _factory = new IntegrationTestWebApplicationFactory(testOutputHelper);
        _client = _factory.CreateClient();

        _sqs = _factory.Services.GetRequiredService<IAmazonSQS>();
        _queueUrl = (await _sqs.GetQueueUrlAsync("trade_gateway_publisher_intra_updates_test.fifo")).QueueUrl;

        await _sqs.PurgeQueueAsync(_queueUrl);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async ValueTask DisposeAsync()
    {
        await WireMockStubber.ResetAsync(_factory.WireMockBaseUrl);
        _client?.Dispose();
        await _factory.DisposeAsync();
    }
}
