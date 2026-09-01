using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Testing;

namespace TradeGatewayPublisher.IntegrationTests.IntraChanges;

[Trait("Category", "IntegrationTest")]
[Collection(NonParallelCollection.Name)]
public class IntraPollingAsbIntegrationTest(IntegrationTestFixture fixture, ITestOutputHelper testOutputHelper) : IAsyncLifetime
{
    private IDisposable? _logCapture;

    private readonly string IntraId = $"intra-test-1-asb-{Random.Shared.Next(1, 100000)}";

    [Fact]
    public async Task IntraPollingJobPolls_AndThenPublishesToAsb()
    {
        if (!fixture.ServiceBusIsEnabled)
        {
            // Nothing more to assert for Service Bus in this environment
            testOutputHelper.WriteLine("Service bus not enabled, test not run");
            return;
        }

        fixture.StartClient();

        // If ASB publishing is feature-switched off, skip ASB check
        var config = fixture.Factory.Services.GetRequiredService<IConfiguration>();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Stub the traces gateway so the polling job will find the update
        await WireMockStubber.StubIntrasAsync(fixture.Factory.WireMockBaseUrl, IntraId, cancellationToken);

        // Read Service Bus configuration and verify message on the queue subscribed to the topic (emulator)
        var tracesOptions = config.GetSection(TracesServiceBusOptions.SectionName).Get<TracesServiceBusOptions>()!;
        var connectionString = tracesOptions.Intra.ConnectionString;
        var topicName = tracesOptions.Intra.TopicName;


        var subscription = "trade-gateway-publisher-intra-test-sub";
        await using var client = new ServiceBusClient(connectionString);
        // Receive from the topic's subscription (receive from topic-subscription pair)
        var receiver = client.CreateReceiver(
            topicName,
            subscription,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock }
        );

        var receivedOnAsb = await WaitHelper.WaitUntilAsync(
            () =>
                ServiceBusUtilities.ServiceBusQueueContainsExpectedAsync(
                        receiver,
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

        receivedOnAsb
            .Should()
            .BeTrue(
                "The Intra polling job should have published a message to the Service Bus topic subscription within 120s"
            );
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
