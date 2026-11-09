using System.Net;
using AwesomeAssertions;
using Defra.TradeImports.Messaging.Publishing;
using Infrastructure.Scheduler;
using Microsoft.Extensions.Logging;
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
                ReferenceDataCompressionThresholdBytes = 1024 * 768,
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
                Arg.Any<string>(),
                Arg.Any<int?>(),
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
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>()
            );

        foreach (var treeId in options.Value.ClassificationTreeIds)
        {
            await _referenceDataClient
                .Received(1)
                .FindCommodityTreeUpdates(
                    treeId,
                    Arg.Any<DateTimeOffset>(),
                    Arg.Any<DateTimeOffset>(),
                    Arg.Any<CancellationToken>()
                );
        }
    }

    [Fact]
    public async Task ExecuteAsync_should_log_warning_and_rethrow_when_validation_exception_is_thrown()
    {
        // Arrange
        var options = TestTracesOptions.TracesUpdatePublisherOptions;
        var logger = Substitute.For<ILogger<TracesClassificationTreeNodeChangesJob>>();
        logger.IsEnabled(LogLevel.Warning).Returns(true);
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://localhost/reference-data/classifications/trees/cheda/nodes"
        );
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            RequestMessage = request,
            Content = new StringContent(
                """
                {
                  "title": "One or more validation errors occurred.",
                  "status": 400,
                  "errors": {
                    "treeId": ["Invalid tree id"]
                  }
                }
                """
            ),
        };

        var apiException = await ApiException.Create(request, HttpMethod.Get, response, new RefitSettings());
        var expectedException = ValidationApiException.Create(apiException);

        var sut = new TracesClassificationTreeNodeChangesJob(_referenceDataClient, _sns, options, logger);

        _referenceDataClient
            .FindCommodityTreeUpdates(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromException<ApiResponse<DefraUNVTDProfileClassificationTreeResponse>>(expectedException));

        // Act
        var ex = await Assert.ThrowsAsync<ValidationApiException>(() =>
            sut.ExecuteAsync(CreateContext(), CancellationToken.None)
        );

        // Assert
        Assert.Same(expectedException, ex);
        await _sns.DidNotReceive()
            .PublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>()
            );

        var logCalls = logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log)).ToList();

        logCalls.Count.Should().Be(1);

        var rendered = logCalls
            .Select(c =>
            {
                var args = c.GetArguments();
                var state = args[2];
                var exception = args[3] as Exception;
                var formatter = args[4];
                var invoke = formatter!.GetType().GetMethod("Invoke")!;
                return (string)invoke.Invoke(formatter, [state, exception])!;
            })
            .ToList();

        rendered.Should().Contain(m => m.Contains("failed validation with validation error"));
    }

    [Fact]
    public async Task ExecuteAsync_should_publish_all_recursive_children()
    {
        // Arrange
        var options = TestTracesOptions.TracesUpdatePublisherOptions;

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
                        Path = "/root",
                        Label = "Root",
                        NodeType = "nomenclature",
                        Selectable = false,
                        Children =
                        [
                            new ClassificationTreeNode
                            {
                                NodeId = "Node-1-1",
                                Path = "/root/a",
                                Label = "Child",
                                NodeType = "nomenclature",
                                Selectable = false,
                                Children =
                                [
                                    new ClassificationTreeNode
                                    {
                                        NodeId = "Node-1-1-1",
                                        Path = "/root/a/b",
                                        Label = "Grandchild",
                                        NodeType = "nomenclature",
                                        Selectable = true,
                                        CnCode = "12345",
                                    },
                                ],
                            },
                        ],
                    },
                ],
                RetrievedAt = new DateTimeOffset(2026, 10, 21, 14, 0, 0, TimeSpan.Zero),
            },
            new RefitSettings()
        );

        _referenceDataClient
            .FindCommodityTreeUpdates(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(response);

        // Act
        await sut.ExecuteAsync(CreateContext(), CancellationToken.None);

        // Assert — root + child + grandchild published once per tree ID
        var expectedCallCount = options.Value.ClassificationTreeIds.Length * 3;
        await _sns.Received(expectedCallCount)
            .PublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Is<Dictionary<string, string>>(headers =>
                    !string.IsNullOrWhiteSpace(headers[ClassificationTreeMessageHeaderKeys.DuplicationId])
                ),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_should_publish_correct_total_count_across_multiple_roots_and_nested_children()
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
                ReferenceDataCompressionThresholdBytes = 1024 * 768,
            }
        );

        var sut = new TracesClassificationTreeNodeChangesJob(
            _referenceDataClient,
            _sns,
            options,
            NullLogger<TracesClassificationTreeNodeChangesJob>.Instance
        );

        // Two root nodes: first has one child (2 total), second is flat (1 total) → 3 total
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
                        Label = "Parent",
                        NodeType = "nomenclature",
                        Selectable = false,
                        Children =
                        [
                            new ClassificationTreeNode
                            {
                                NodeId = "Node-1-1",
                                Path = "/root/a/b",
                                Label = "Child",
                                NodeType = "nomenclature",
                                Selectable = true,
                                CnCode = "11111",
                            },
                        ],
                    },
                    new ClassificationTreeNode
                    {
                        NodeId = "Node-2",
                        Path = "/root/b",
                        Label = "Standalone",
                        NodeType = "nomenclature",
                        Selectable = true,
                        CnCode = "22222",
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

        // Assert — parent + child + standalone = 3 publishes total
        await _sns.Received(3)
            .PublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>()
            );
    }

    private static JobContext CreateContext(WatermarkContext? watermark = null)
    {
        var context = new JobContext("JobId", "TestJob", new JobSettings());

        watermark ??= new WatermarkContext(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow);

        context.Set(watermark);

        return context;
    }
}
