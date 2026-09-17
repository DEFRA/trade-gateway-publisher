using System.Net;
using AwesomeAssertions;
using Testing;

namespace TradeGatewayPublisher.IntegrationTests.Endpoints;

[Trait("Category", "IntegrationTest")]
[Collection(NonParallelCollection.Name)]
public class ForceChedPublishEndpointTests(IntegrationTestFixture fixture, ITestOutputHelper testOutputHelper)
    : IAsyncLifetime
{
    private readonly string ChedId = $"ched-test-force-{Random.Shared.Next(1, 100000)}";

    private IDisposable? _logCapture;

    [Fact]
    public async Task Get_WhenCertificateExists_ReturnsOkAndPublishesToSns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await fixture.Client.GetAsync($"ched/{ChedId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

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
            TimeSpan.FromSeconds(60),
            TimeSpan.FromMilliseconds(500),
            cancellationToken
        );

        received.Should().BeTrue("Forcing a CHED publish should result in the certificate being published to SNS");
    }

    [Fact]
    public async Task Get_WhenCertificateDoesNotExist_ReturnsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var missingChedId = $"{ChedId}-missing";

        var response = await fixture.Client.GetAsync($"ched/{missingChedId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public async ValueTask InitializeAsync()
    {
        _logCapture = TestOutputHelperSink.Capture(testOutputHelper);

        await WireMockStubber.ResetAsync(fixture.Factory.WireMockBaseUrl);
        await WireMockStubber.StubChedsAsync(fixture.Factory.WireMockBaseUrl, ChedId, CancellationToken.None);

        await fixture.AmazonSqs.PurgeQueueAsync(fixture.TestChedQueueUrl);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async ValueTask DisposeAsync()
    {
        _logCapture?.Dispose();
        GC.SuppressFinalize(this);
    }
}
