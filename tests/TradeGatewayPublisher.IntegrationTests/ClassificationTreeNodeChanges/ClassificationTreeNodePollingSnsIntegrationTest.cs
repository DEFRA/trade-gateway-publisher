using AwesomeAssertions;

using Testing;

namespace TradeGatewayPublisher.IntegrationTests.ClassificationTreeNodeChanges;

[Trait("Category", "IntegrationTest")]
[Collection(NonParallelCollection.Name)]
public class ClassificationTreeNodePollingSnsIntegrationTest(
    IntegrationTestFixture fixture,
    ITestOutputHelper testOutputHelper
) : IAsyncLifetime
{
    private readonly string NodeId = $"Node-{Random.Shared.Next(1, 100000)}";

    private IDisposable? _logCapture;

    [Fact]
    public async Task ClassificationTreeNodePollingJobPolls_AndThenPublishesToSns()
    {
        fixture.StartClient();

        var cancellationToken = TestContext.Current.CancellationToken;

        var received = await WaitHelper.WaitUntilAsync(
            () =>
                SnsUtilities
                    .SnsQueueContainsExpectedAsync(
                        fixture.AmazonSqs,
                        fixture.TestClassificationTreeNodeQueueUrl,
                        NodeId,
                        testOutputHelper,
                        cancellationToken
                    )
                    .GetAwaiter()
                    .GetResult(),
            TimeSpan.FromSeconds(120),
            TimeSpan.FromMilliseconds(500),
            cancellationToken
        );

        received
            .Should()
            .BeTrue(
                "The ClassificationTreeNode polling job should have polled Traces and published to SNS within 120s"
            );
    }

    public async ValueTask InitializeAsync()
    {
        _logCapture = TestOutputHelperSink.Capture(testOutputHelper);

        await WireMockStubber.ResetAsync(fixture.Factory.WireMockBaseUrl);
        await WireMockStubber.StubClassificationTreeNodesAsync(
            fixture.Factory.WireMockBaseUrl,
            NodeId,
            "cheda",
            CancellationToken.None
        );

        await fixture.DeleteDatabaseAsync();
        await fixture.AmazonSqs.PurgeQueueAsync(fixture.TestClassificationTreeNodeQueueUrl);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async ValueTask DisposeAsync()
    {
        _logCapture?.Dispose();
        GC.SuppressFinalize(this);
    }
}