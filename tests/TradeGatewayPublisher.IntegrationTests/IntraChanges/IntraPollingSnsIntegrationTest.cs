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

        fixture.StartClient();

        var received = await WaitHelper.WaitUntilAsync(
            () =>
                SnsUtilities
                    .SnsQueueContainsExpectedAsync(
                        fixture.AmazonSqs,
                        fixture.TestIntraQueueUrl,
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

    public async ValueTask InitializeAsync()
    {
        _logCapture = TestOutputHelperSink.Capture(testOutputHelper);

        await WireMockStubber.ResetAsync(fixture.Factory.WireMockBaseUrl);
        await WireMockStubber.StubIntrasAsync(fixture.Factory.WireMockBaseUrl, IntraId, CancellationToken.None);

        await fixture.DeleteDatabaseAsync();
        await fixture.AmazonSqs.PurgeQueueAsync(fixture.TestIntraQueueUrl);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async ValueTask DisposeAsync()
    {
        _logCapture?.Dispose();
    }
}
