using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging;
using Infrastructure.Watermark;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testing;

namespace TradeGatewayPublisher.IntegrationTests.IntraChanges;

[Trait("Category", "IntegrationTest")]
[Collection(NonParallelCollection.Name)]
public class IntraPollingAsbIntegrationTest : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _testOutputHelper;
    private IDisposable? _logCapture;

    private const string IntraId = "intra-test-1-asb";

    public IntraPollingAsbIntegrationTest(IntegrationTestFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task IntraPollingJobPolls_AndThenPublishesToAsb()
    {
        if (!_fixture.ServiceBusIsEnabled)
        {
            // Nothing more to assert for Service Bus in this environment
            _testOutputHelper.WriteLine("Service bus not enabled, test not run");
            return;
        }

        // If ASB publishing is feature-switched off, skip ASB check
        var config = _fixture.Factory.Services.GetRequiredService<IConfiguration>();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Stub the traces gateway so the polling job will find the update
        await WireMockStubber.StubAsync(_fixture.Factory.WireMockBaseUrl, IntraId, cancellationToken);

        // Read Service Bus configuration and verify message on the queue subscribed to the topic (emulator)
        var tracesOptions = config.GetSection(TracesServiceBusOptions.SectionName).Get<TracesServiceBusOptions>()!;
        var connectionString = tracesOptions.Intra.ConnectionString;
        var topicName = tracesOptions.Intra.TopicName;

        var subscription = "trade-gateway-publisher-intra-test-sub";

        var receivedOnAsb = await WaitHelper.WaitUntilAsync(
            () =>
                ServiceBusQueueContainsExpectedAsync(
                        connectionString,
                        topicName,
                        subscription,
                        IntraId,
                        _testOutputHelper,
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

    private static async Task<bool> ServiceBusQueueContainsExpectedAsync(
        string connectionString,
        string topicName,
        string subscription,
        string expectedId,
        ITestOutputHelper testOutputHelper,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await using var client = new ServiceBusClient(connectionString);
            // Receive from the topic's subscription (receive from topic-subscription pair)
            var receiver = client.CreateReceiver(
                topicName,
                subscription,
                new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock }
            );

            var messages = await receiver.ReceiveMessagesAsync(
                maxMessages: 10,
                maxWaitTime: TimeSpan.FromSeconds(5),
                cancellationToken: cancellationToken
            );

            if (messages == null || messages.Count == 0)
                return false;

            foreach (var msg in messages)
            {
                var body = msg.Body.ToString();
                try
                {
                    await receiver.CompleteMessageAsync(msg, cancellationToken);
                }
                catch (Exception ex)
                {
                    // best-effort complete; ignore if emulator behaves differently
                    testOutputHelper.WriteLine(ex.Message);
                }

                if (body.Contains(expectedId, StringComparison.Ordinal))
                    return true;
            }
        }
        catch (Exception ex)
        {
            testOutputHelper.WriteLine(ex.Message);
            // If the emulator isn't reachable or the receiver fails, treat as not received for retry loop
            return false;
        }

        return false;
    }

    private HttpClient? _client;

    public async ValueTask InitializeAsync()
    {
        _logCapture = TestOutputHelperSink.Capture(_testOutputHelper);

        _client = _fixture.Factory.CreateClient();

        await _fixture.AmazonSqs.PurgeQueueAsync(_fixture.QueueUrl);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async ValueTask DisposeAsync()
    {
        await WireMockStubber.ResetAsync(_fixture.Factory.WireMockBaseUrl);
        _client?.Dispose();
        _logCapture?.Dispose();
    }
}
