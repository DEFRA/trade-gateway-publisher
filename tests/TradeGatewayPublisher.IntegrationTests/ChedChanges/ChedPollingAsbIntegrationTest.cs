using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testing;

namespace TradeGatewayPublisher.IntegrationTests.ChedChanges;

[Trait("Category", "IntegrationTest")]
[Collection(NonParallelCollection.Name)]
public class ChedPollingAsbIntegrationTest(IntegrationTestFixture fixture, ITestOutputHelper testOutputHelper)
    : IAsyncLifetime
{
    private const string Subscription = "trade-gateway-publisher-ched-test-sub";

    private IDisposable? _logCapture;

    private readonly string ChedId = $"ched-test-1-asb-{Random.Shared.Next(1, 100000)}";

    [Fact]
    public async Task ChedPollingJobPolls_AndThenPublishesToAsb()
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
        await WireMockStubber.StubChedsAsync(fixture.Factory.WireMockBaseUrl, ChedId, cancellationToken);

        // Read Service Bus configuration and verify message on the queue subscribed to the topic (emulator)
        var tracesOptions = config.GetSection(TracesServiceBusOptions.SectionName).Get<TracesServiceBusOptions>()!;
        var connectionString = tracesOptions.ConnectionString;
        var topicName = tracesOptions.Ched.TopicName;

        await using var client = new ServiceBusClient(connectionString);
        // Receive from the topic's subscription (receive from topic-subscription pair)
        var receiver = client.CreateReceiver(
            topicName,
            Subscription,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock }
        );

        var receivedOnAsb = await WaitHelper.WaitUntilAsync(
            () =>
                ServiceBusUtilities
                    .ServiceBusQueueContainsExpectedAsync(receiver, ChedId, testOutputHelper, cancellationToken)
                    .GetAwaiter()
                    .GetResult(),
            TimeSpan.FromSeconds(120),
            TimeSpan.FromMilliseconds(500),
            cancellationToken
        );

        receivedOnAsb
            .Should()
            .BeTrue(
                "The Ched polling job should have published a message to the Service Bus topic subscription within 120s"
            );
    }

    [Fact]
    public async Task MessagePublishedToAsbWithoutMessageTtlConfigured_UsesDefaultTtl()
    {
        if (!fixture.ServiceBusIsEnabled)
        {
            testOutputHelper.WriteLine("Service bus not enabled, test not run");
            return;
        }

        fixture.StartClient();

        var config = fixture.Factory.Services.GetRequiredService<IConfiguration>();
        var cancellationToken = TestContext.Current.CancellationToken;

        await WireMockStubber.StubChedsAsync(fixture.Factory.WireMockBaseUrl, ChedId, cancellationToken);

        var tracesOptions = config.GetSection(TracesServiceBusOptions.SectionName).Get<TracesServiceBusOptions>()!;
        var connectionString = tracesOptions.ConnectionString;
        var topicName = tracesOptions.Ched.TopicName;

        await using var client = new ServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(
            topicName,
            Subscription,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock }
        );

        // Default Message Ttl is effectively never expire
        Func<ServiceBusReceivedMessage, bool> messageVerifier = message => message.ExpiresAt <= DateTime.MaxValue;

        var receivedOnAsb = await WaitHelper.WaitUntilAsync(
            () =>
                ServiceBusUtilities
                    .ServiceBusQueueContainsExpectedAsync(
                        receiver,
                        ChedId,
                        testOutputHelper,
                        cancellationToken,
                        true,
                        messageVerifier
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
                "The Ched polling job should have published a message using the default message TTL to the Service Bus topic subscription within 120s"
            );

        // Once message moves through the bus lifecycle and is received, it uses the max ceiling of the Queue TTL
        Func<ServiceBusReceivedMessage, bool> brokeredMessageVerifier = message =>
        {
            var expectedExpiry = DateTime.UtcNow.AddHours(1); // 1 hour for the default Service Bus Emulator
            return message.ExpiresAt <= expectedExpiry;
        };

        var brokeredMessage = await WaitHelper.WaitUntilAsync(
            () =>
                ServiceBusUtilities
                    .ServiceBusQueueContainsExpectedAsync(
                        receiver,
                        ChedId,
                        testOutputHelper,
                        cancellationToken,
                        false,
                        brokeredMessageVerifier
                    )
                    .GetAwaiter()
                    .GetResult(),
            TimeSpan.FromSeconds(120),
            TimeSpan.FromMilliseconds(500),
            cancellationToken
        );

        brokeredMessage
            .Should()
            .BeTrue(
                "The Ched polling job should have published a message using the queue TTL to the Service Bus topic subscription within 120s"
            );
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
        GC.SuppressFinalize(this);
    }
}
