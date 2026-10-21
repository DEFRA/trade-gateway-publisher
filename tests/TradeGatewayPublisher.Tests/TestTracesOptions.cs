using Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Tests
{
    internal static class TestTracesOptions
    {
        public static IOptions<TracesServiceBusOptions> TracesServiceBusOptions =>
            Options.Create(
                new TracesServiceBusOptions
                {
                    Intra = new ServiceBusTopic { TopicName = "intra-topic" },
                    Ched = new ServiceBusTopic { TopicName = "ched-topic" },
                    ClassificationTreeNode = new ServiceBusTopic { TopicName = "classificationtreenode-topic" },
                    ConnectionString = "Endpoint=sb://127.0.0.1;",
                }
            );

        public static IOptions<TracesUpdatePublisherOptions> TracesUpdatePublisherOptions =>
            Options.Create(
                new TracesUpdatePublisherOptions
                {
                    IntraTopicArn = "test-intra-topic",
                    IntraInternalTopicArn = "test-intra-internal-topic",
                    ChedTopicArn = "test-ched-topic",
                    ChedInternalTopicArn = "test-ched-internal-topic",
                    ClassificationTreeNodeTopicArn = "test-classification-tree-node-topic",
                    ClassificationTreeNodeInternalTopicArn = "test-classification-tree-node-internal-topic",
                    ClassificationTreeIds = ["cheda", "chedp", "intra_trade"],
                }
            );
    }
}
