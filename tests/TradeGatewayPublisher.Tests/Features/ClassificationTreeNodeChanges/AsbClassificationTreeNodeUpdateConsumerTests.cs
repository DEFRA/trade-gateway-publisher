using System.Text.Json;
using Amazon.SQS.Model;
using AwesomeAssertions;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trade.Gateway.Api.Contract.Events;
using TradeGatewayPublisher.Features.ClassificationTreeNodeChanges;

namespace TradeGatewayPublisher.Tests.Features.ClassificationTreeNodeChanges;

public class AsbClassificationTreeNodeUpdateConsumerTests
{
    private readonly IAsbPublisher _asbPublisher = Substitute.For<IAsbPublisher>();
    private readonly ILogger<AsbClassificationTreeNodeUpdateConsumer> _logger = Substitute.For<
        ILogger<AsbClassificationTreeNodeUpdateConsumer>
    >();

    private const string TestEventId = "00000000-0000-0000-0000-111111111111";
    private readonly AsbClassificationTreeNodeUpdateConsumer _sut;

    public AsbClassificationTreeNodeUpdateConsumerTests()
    {
        var options = Options.Create(
            new TracesServiceBusOptions
            {
                Intra = new ServiceBusTopic { TopicName = "intra-topic" },
                Ched = new ServiceBusTopic { TopicName = "ched-topic" },
                ClassificationTreeNode = new ServiceBusTopic { TopicName = "classificationtreenode-topic" },
                ConnectionString = "Endpoint=sb://127.0.0.1;",
            }
        );

        _sut = new AsbClassificationTreeNodeUpdateConsumer(_asbPublisher, options, _logger);
    }

    [Fact]
    public async Task ConsumeAsync_should_publish_to_service_bus_with_expected_values()
    {
        // Arrange
        var ctx = CreateContext("msg-classification");
        _logger.IsEnabled(LogLevel.Information).Returns(true);

        // Act
        await _sut.ConsumeAsync(ctx, CancellationToken.None);

        // Assert
        await _asbPublisher
            .Received(1)
            .PublishAsync(
                Arg.Is<string>(topic => topic == "classificationtreenode-topic"),
                Arg.Is<string>(id => id == ctx.MessageId),
                Arg.Is<Dictionary<string, string>>(headers =>
                    headers.ContainsKey(ClassificationTreeMessageHeaderKeys.TreeId)
                    && headers[ClassificationTreeMessageHeaderKeys.TreeId] == "cheda"
                    && headers.ContainsKey(ClassificationTreeMessageHeaderKeys.DuplicationId)
                    && headers[ClassificationTreeMessageHeaderKeys.DuplicationId] == "node-1_1720000000000"
                ),
                Arg.Is<string>(body => body == ctx.Body),
                Arg.Any<CancellationToken>()
            );

        var logCalls = _logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log)).ToList();

        logCalls.Count.Should().Be(2);

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

        rendered.Should().Contain(m => m.Contains("Publishing Classification Tree Node event"));

        rendered.Should().Contain(m => m.Contains("Published Classification Tree Node event"));
    }

    private static MessageContext CreateContext(string id) =>
        new()
        {
            Message = new Message
            {
                Body = JsonSerializer.Serialize(
                    new EventEnvelope<object>
                    {
                        EventId = Guid.Parse(TestEventId),
                        AggregateId = "123",
                        AggregateType = "aggType",
                        EventType = "eventType",
                        Data = """
                        {"test":"data"}
                        """,
                        Metadata = new EventEnvelopeMetadata
                        {
                            CorrelationId = "00000000-0000-0000-0000-222222222222",
                            SchemaUri = new Uri("http://uri"),
                            SchemaVersion = "1.0",
                        },
                        SubType = "subtype",
                        Timestamp = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    }
                ),
                MessageId = id,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    [ClassificationTreeMessageHeaderKeys.TreeId] = new() { DataType = "String", StringValue = "cheda" },
                    [ClassificationTreeMessageHeaderKeys.DuplicationId] = new()
                    {
                        DataType = "String",
                        StringValue = "node-1_1720000000000",
                    },
                },
            },
            QueueUrl = "queue-url",
            ConsumerType = typeof(AsbClassificationTreeNodeUpdateConsumer),
        };
}
