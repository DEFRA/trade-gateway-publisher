using System.Text.Json;
using Amazon.SQS.Model;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trade.Gateway.Api.Contract.Events;
using TradeGatewayPublisher.Features.ChedChanges;

namespace TradeGatewayPublisher.Tests.Features.ChedChanges;

public class AsbChedUpdateConsumerTests
{
    private readonly IAsbPublisher _asbPublisher = Substitute.For<IAsbPublisher>();
    private readonly ILogger<AsbChedUpdateConsumer> _logger = Substitute.For<ILogger<AsbChedUpdateConsumer>>();
    private readonly AsbChedUpdateConsumer _sut;
    private const string TestEventId = "00000000-0000-0000-0000-111111111111";

    public AsbChedUpdateConsumerTests()
    {
        var options = Options.Create(
            new TracesServiceBusOptions
            {
                Intra = new ServiceBusTopic
                {
                    TopicName = "intra-topic",
                    ConnectionString = "Endpoint=sb://127.0.0.1;",
                },
                Ched = new ServiceBusTopic { TopicName = "ched-topic", ConnectionString = "Endpoint=sb://127.0.0.1;" },
            }
        );

        _sut = new AsbChedUpdateConsumer(_asbPublisher, options, _logger);
    }

    [Fact]
    public async Task ConsumeAsync_should_publish_to_service_bus_with_expected_values()
    {
        // Arrange
        var ctx = CreateContext("msg-ched");

        // Act
        await _sut.ConsumeAsync(ctx, CancellationToken.None);

        // Assert
        await _asbPublisher
            .Received(1)
            .PublishAsync(
                Arg.Is<string>(q => q == "ched-topic"),
                Arg.Is<string>(id => id == ctx.MessageId),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Is<string>(body => body == ctx.Body),
                Arg.Any<CancellationToken>()
            );

        // Expect two log entries that include the TestEventId in their message/state
        _logger
            .Received(2)
            .Log(
                Arg.Any<LogLevel>(),
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o!.ToString()!.Contains(TestEventId)),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
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
            },
            QueueUrl = "queue-url",
            ConsumerType = typeof(AsbChedUpdateConsumer),
        };
}
