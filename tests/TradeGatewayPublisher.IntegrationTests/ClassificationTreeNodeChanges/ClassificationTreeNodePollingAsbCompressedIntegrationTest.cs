using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testing;
using Trade.Gateway.Api.Contract.ReferenceData;

namespace TradeGatewayPublisher.IntegrationTests.ClassificationTreeNodeChanges;

[Trait("Category", "IntegrationTest")]
[Collection(NonParallelCollection.Name)]
public class ClassificationTreeNodePollingAsbCompressedIntegrationTest(
    IntegrationTestFixture fixture,
    ITestOutputHelper testOutputHelper
) : IAsyncLifetime
{
    private IDisposable? _logCapture;

    private readonly string NodeId = $"Node-{Random.Shared.Next(1, 100000)}";
    private const int CompressionThresholdBytes = 128 * 1024;
    private const int ServiceBusMaxBytes = 256 * 1024;

    private readonly string LargeNodeLabel = BuildNodeLabelBetweenThresholdAndServiceBusMax();

    [Fact]
    public async Task ClassificationTreeNodeJobPolls_AndThenPublishesToAsb_WhenInternalMessageIsCompressed()
    {
        if (!fixture.ServiceBusIsEnabled)
        {
            testOutputHelper.WriteLine("Service bus not enabled, test not run");
            return;
        }

        var nodeSizeBytes = Encoding.UTF8.GetByteCount(
            JsonSerializer.Serialize(
                new ClassificationTreeNode
                {
                    NodeId = NodeId,
                    Path = "/root/a",
                    Label = LargeNodeLabel,
                    NodeType = "nomenclature",
                    Selectable = true,
                    CnCode = "12345",
                }
            )
        );

        nodeSizeBytes.Should().BeGreaterThan(CompressionThresholdBytes);
        nodeSizeBytes.Should().BeLessThan(ServiceBusMaxBytes);

        fixture.StartClient();

        var config = fixture.Factory.Services.GetRequiredService<IConfiguration>();
        var cancellationToken = TestContext.Current.CancellationToken;

        var tracesOptions = config.GetSection(TracesServiceBusOptions.SectionName).Get<TracesServiceBusOptions>()!;
        var connectionString = tracesOptions.ConnectionString;
        var topicName = tracesOptions.ClassificationTreeNode.TopicName;

        var subscription = "trade-gateway-publisher-refdata-test-sub";

        await using var client = new ServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(
            topicName,
            subscription,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock }
        );

        var receivedOnAsb = await WaitHelper.WaitUntilAsync(
            () =>
                ServiceBusUtilities
                    .ServiceBusQueueContainsExpectedAsync(receiver, NodeId, testOutputHelper, cancellationToken)
                    .GetAwaiter()
                    .GetResult(),
            TimeSpan.FromSeconds(120),
            TimeSpan.FromMilliseconds(500),
            cancellationToken
        );

        receivedOnAsb
            .Should()
            .BeTrue(
                "The Classification Tree Node polling job should have published a compressed internal message and produced an ASB event within 120s"
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
            LargeNodeLabel,
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

    private static string BuildNodeLabelBetweenThresholdAndServiceBusMax()
    {
        var targetPayloadBytes = (CompressionThresholdBytes + ServiceBusMaxBytes) / 2;
        var estimatedBaseBytes = 512;
        var labelLength = Math.Max(1, targetPayloadBytes - estimatedBaseBytes);

        while (true)
        {
            var label = new string('L', labelLength);
            var sizeBytes = Encoding.UTF8.GetByteCount(
                JsonSerializer.Serialize(
                    new ClassificationTreeNode
                    {
                        NodeId = "Node-1",
                        Path = "/root/a",
                        Label = label,
                        NodeType = "nomenclature",
                        Selectable = true,
                        CnCode = "12345",
                    }
                )
            );

            if (sizeBytes > CompressionThresholdBytes && sizeBytes < ServiceBusMaxBytes)
            {
                return label;
            }

            if (sizeBytes <= CompressionThresholdBytes)
            {
                labelLength += (CompressionThresholdBytes - sizeBytes) + 256;
            }
            else
            {
                labelLength -= (sizeBytes - ServiceBusMaxBytes) + 256;
            }

            if (labelLength < 1)
            {
                labelLength = 1;
            }
        }
    }
}
