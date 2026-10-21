using System.Net;
using Infrastructure.Scheduler;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Refit;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.ReferenceData;
using TradeGatewayPublisher.Config;
using TradeGatewayPublisher.Features.ClassificationTreeNodeChanges;

namespace TradeGatewayPublisher.Tests.Features.ClassificationTreeNodeChanges;

public class TracesClassificationTreeNodeChangesJobTests
{
    private readonly IReferenceDataClient _referenceDataClient = Substitute.For<IReferenceDataClient>();
    private readonly ISnsPublisher _sns = Substitute.For<ISnsPublisher>();

    [Fact]
    public async Task ExecuteAsync_should_publish_updates_with_expected_headers_and_duplication_id()
    {
        // Arrange
        var options = Options.Create(
            new TracesUpdatePublisherOptions
            {
                IntraTopicArn = "test-intra-topic",
                IntraInternalTopicArn = "test-intra-internal-topic",
                ChedTopicArn = "test-ched-topic",
                ChedInternalTopicArn = "test-ched-internal-topic",
                ClassificationTreeNodeTopicArn = "test-classification-tree-node-topic",
                ClassificationTreeNodeInternalTopicArn = "test-classification-tree-node-internal-topic",
                ClassificationTreeIds = ["cheda"],
            }
        );

        var sut = new TracesClassificationTreeNodeChangesJob(
            _referenceDataClient,
            _sns,
            options,
            NullLogger<TracesClassificationTreeNodeChangesJob>.Instance
        );

        var response = new ApiResponse<DefraUNVTDProfileClassificationTreeResponse>(
            new HttpResponseMessage(HttpStatusCode.OK),
            new DefraUNVTDProfileClassificationTreeResponse
            {
                Nodes =
                [
                    new ClassificationTreeNode
                    {
                        NodeId = "Node-1",
                        Path = "/root/a",
                        Label = "Mules",
                        NodeType = "nomenclature",
                        Selectable = true,
                        CnCode = "12345",
                    },
                ],
                RetrievedAt = new DateTimeOffset(2026, 10, 21, 14, 0, 0, TimeSpan.Zero),
            },
            new RefitSettings()
        );

        _referenceDataClient
            .FindCommodityTreeUpdates(
                "cheda",
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(response);

        // Act
        await sut.ExecuteAsync(CreateContext(), CancellationToken.None);

        // Assert
        await _sns.Received(1)
            .PublishAsync(
                "test-classification-tree-node-internal-topic",
                Arg.Any<string>(),
                Arg.Is<Dictionary<string, string>>(headers =>
                    headers[ClassificationTreeMessageHeaderKeys.TreeId] == "cheda"
                    && !string.IsNullOrWhiteSpace(headers[ClassificationTreeMessageHeaderKeys.DuplicationId])
                ),
                Arg.Any<string>(),
                Arg.Is<string>(duplicationId => !string.IsNullOrWhiteSpace(duplicationId)),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_should_not_publish_when_all_trees_return_no_updates()
    {
        // Arrange
        var options = TestTracesOptions.TracesUpdatePublisherOptions;

        var sut = new TracesClassificationTreeNodeChangesJob(
            _referenceDataClient,
            _sns,
            options,
            NullLogger<TracesClassificationTreeNodeChangesJob>.Instance
        );

        _referenceDataClient
            .FindCommodityTreeUpdates(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new ApiResponse<DefraUNVTDProfileClassificationTreeResponse>(
                    new HttpResponseMessage(HttpStatusCode.OK),
                    new DefraUNVTDProfileClassificationTreeResponse
                    {
                        Nodes = [],
                        RetrievedAt = new DateTimeOffset(2026, 10, 21, 14, 0, 0, TimeSpan.Zero),
                    },
                    new RefitSettings()
                )
            );

        // Act
        await sut.ExecuteAsync(CreateContext(), CancellationToken.None);

        // Assert
        await _sns.DidNotReceive()
            .PublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );

        foreach (var treeId in options.Value.ClassificationTreeIds)
        {
            await _referenceDataClient.Received(1)
                .FindCommodityTreeUpdates(
                    treeId,
                    Arg.Any<DateTimeOffset>(),
                    Arg.Any<DateTimeOffset>(),
                    Arg.Any<CancellationToken>()
                );
        }
    }

    private static JobContext CreateContext(WatermarkContext? watermark = null)
    {
        var context = new JobContext("JobId", "TestJob", new JobSettings());

        watermark ??= new WatermarkContext(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow);

        context.Set(watermark);

        return context;
    }
}
