using System.Net;
using System.Text.Json;
using Amazon.SQS.Model;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Refit;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.ReferenceData;
using TradeGatewayPublisher.Features.ClassificationTreeNodeChanges;

namespace TradeGatewayPublisher.Tests.Features.ClassificationTreeNodeChanges;

public class ClassificationTreeNodeUpdateConsumerTests
{
    private readonly ITracesGatewayClient _gateway = Substitute.For<ITracesGatewayClient>();
    private readonly ISnsPublisher _sns = Substitute.For<ISnsPublisher>();
    private readonly ClassificationTreeNodeUpdateConsumer _sut;

    public ClassificationTreeNodeUpdateConsumerTests()
    {
        _gateway
            .GetClassificationTreeNodeDetail("cheda", "Node-1", Arg.Any<CancellationToken>())
            .Returns(
                new ApiResponse<DefraUNVTDProfileClassificationTreeNodeDetailResponse>(
                    new HttpResponseMessage(HttpStatusCode.OK),
                    new DefraUNVTDProfileClassificationTreeNodeDetailResponse
                    {
                        TreeId = "cheda",
                        NodeId = "Node-1",
                        NodePath = "/root/a",
                        RetrievedAt = DateTimeOffset.UtcNow,
                        Node = new DefraUNVTDProfileClassificationTreeNodeDetailResponseNode
                        {
                            CnCode = "12345",
                            Label = "Mules",
                            NodeType = "nomenclature",
                            Selectable = true,
                        },
                    },
                    new RefitSettings()
                )
            );

        _sut = new ClassificationTreeNodeUpdateConsumer(
            _gateway,
            _sns,
            TestTracesOptions.TracesUpdatePublisherOptions,
            NullLogger<ClassificationTreeNodeUpdateConsumer>.Instance
        );
    }

    [Fact]
    public async Task ConsumeAsync_should_fetch_detail_and_publish_with_duplication_id()
    {
        // Arrange
        const string expectedTreeId = "cheda";
        const string expectedDuplicationId = "node-dup-id";
        var context = CreateContext("Node-1", expectedTreeId, expectedDuplicationId);

        // Act
        await _sut.ConsumeAsync(context, CancellationToken.None);

        // Assert
        await _gateway
            .Received(1)
            .GetClassificationTreeNodeDetail(expectedTreeId, "Node-1", Arg.Any<CancellationToken>());

        await _sns.Received(1)
            .PublishAsync(
                TestTracesOptions.TracesUpdatePublisherOptions.Value.ClassificationTreeNodeTopicArn,
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                expectedDuplicationId,
                Arg.Any<CancellationToken>()
            );
    }

    private static MessageContext CreateContext(string nodeId, string treeId, string duplicationId) =>
        new()
        {
            Message = new Message
            {
                Body = JsonSerializer.Serialize(
                    new ClassificationTreeNode
                    {
                        NodeId = nodeId,
                        Path = "/root/a",
                        Label = "Mules",
                        NodeType = "nomenclature",
                        Selectable = true,
                        CnCode = "12345",
                    }
                ),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {
                        ClassificationTreeMessageHeaderKeys.TreeId,
                        new MessageAttributeValue { DataType = "String", StringValue = treeId }
                    },
                    {
                        ClassificationTreeMessageHeaderKeys.DuplicationId,
                        new MessageAttributeValue { DataType = "String", StringValue = duplicationId }
                    },
                },
            },
            QueueUrl = "queue-url",
            ConsumerType = typeof(ClassificationTreeNodeUpdateConsumer),
        };
}
