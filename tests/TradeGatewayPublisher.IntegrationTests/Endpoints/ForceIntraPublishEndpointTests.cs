using System.Net;
using AwesomeAssertions;
using Testing;

namespace TradeGatewayPublisher.IntegrationTests.Endpoints;

[Trait("Category", "IntegrationTest")]
[Collection(NonParallelCollection.Name)]
public class ForceIntraPublishEndpointTests(IntegrationTestFixture fixture, ITestOutputHelper testOutputHelper)
    : IAsyncLifetime
{
    private readonly string IntraId = $"intra-test-force-{Random.Shared.Next(1, 100000)}";

    private IDisposable? _logCapture;

    [Fact]
    public async Task Get_WhenCertificateExists_ReturnsOkAndPublishesToSns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await fixture.Client.GetAsync($"intra/{IntraId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

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
            TimeSpan.FromSeconds(60),
            TimeSpan.FromMilliseconds(500),
            cancellationToken
        );

        received.Should().BeTrue("Forcing an Intra publish should result in the certificate being published to SNS");
    }

    [Fact]
    public async Task Get_WhenCertificateDoesNotExist_ReturnsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var missingIntraId = $"{IntraId}-missing";

        var response = await fixture.Client.GetAsync($"intra/{missingIntraId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public async ValueTask InitializeAsync()
    {
        _logCapture = TestOutputHelperSink.Capture(testOutputHelper);

        await WireMockStubber.ResetAsync(fixture.Factory.WireMockBaseUrl);
        await WireMockStubber.StubIntrasAsync(fixture.Factory.WireMockBaseUrl, IntraId, CancellationToken.None);

        await fixture.AmazonSqs.PurgeQueueAsync(fixture.TestIntraQueueUrl);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async ValueTask DisposeAsync()
    {
        _logCapture?.Dispose();
        GC.SuppressFinalize(this);
    }
}
