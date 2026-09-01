using Amazon.SQS;
using Amazon.SQS.Model;
using AwesomeAssertions;
using Testing;

namespace TradeGatewayPublisher.IntegrationTests.ChedChanges;

[Trait("Category", "IntegrationTest")]
[Collection(NonParallelCollection.Name)]
public class ChedPollingSnsIntegrationTest(IntegrationTestFixture fixture, ITestOutputHelper testOutputHelper)
    : IAsyncLifetime
{
    private readonly string ChedId = $"ched-test-1-aws-{Random.Shared.Next(1, 100000)}";

    private IDisposable? _logCapture;

    [Fact]
    public async Task ChedPollingJobPolls_AndThenPublishesToSns()
    {
        fixture.StartClient();

        var cancellationToken = TestContext.Current.CancellationToken;

        var received = await WaitHelper.WaitUntilAsync(
            () =>
                SnsUtilities
                    .SnsQueueContainsExpectedAsync(
                        fixture.AmazonSqs,
                        fixture.TestChedQueueUrl,
                        ChedId,
                        testOutputHelper,
                        cancellationToken
                    )
                    .GetAwaiter()
                    .GetResult(),
            TimeSpan.FromSeconds(120),
            TimeSpan.FromMilliseconds(500),
            cancellationToken
        );

        received.Should().BeTrue("The Ched polling job should have polled Traces and published to SNS within 120s");
    }

    public async ValueTask InitializeAsync()
    {
        _logCapture = TestOutputHelperSink.Capture(testOutputHelper);

        await WireMockStubber.ResetAsync(fixture.Factory.WireMockBaseUrl);
        await WireMockStubber.StubChedsAsync(fixture.Factory.WireMockBaseUrl, ChedId, CancellationToken.None);

        await fixture.DeleteDatabaseAsync();
        await fixture.AmazonSqs.PurgeQueueAsync(fixture.TestChedQueueUrl);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async ValueTask DisposeAsync()
    {
        _logCapture?.Dispose();
    }
}
