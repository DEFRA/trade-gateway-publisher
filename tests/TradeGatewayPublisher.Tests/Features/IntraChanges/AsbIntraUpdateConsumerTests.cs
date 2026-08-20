using Amazon.SQS.Model;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TradeGatewayPublisher.Features.IntraChanges;

namespace TradeGatewayPublisher.Tests.Features.IntraChanges;

public class AsbIntraUpdateConsumerTests
{
    private readonly IAsbPublisher _asbPublisher = Substitute.For<IAsbPublisher>();
    private readonly AsbIntraUpdateConsumer _sut;

    public AsbIntraUpdateConsumerTests()
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

        _sut = new AsbIntraUpdateConsumer(_asbPublisher, options, NullLogger<AsbIntraUpdateConsumer>.Instance);
    }

    [Fact]
    public async Task ConsumeAsync_should_publish_to_service_bus_with_expected_values()
    {
        // Arrange
        var ctx = CreateContext("msg-1");

        // Act
        await _sut.ConsumeAsync(ctx, CancellationToken.None);

        // Assert
        await _asbPublisher
            .Received(1)
            .PublishAsync(
                Arg.Is<string>(q => q == "intra-topic"),
                Arg.Is<string>(id => id == ctx.MessageId),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Is<string>(body => body == ctx.Body),
                Arg.Any<CancellationToken>()
            );
    }

    private static MessageContext CreateContext(string id) =>
        new()
        {
            Message = new Message { Body = "{\"id\":\"1\"}", MessageId = id },
            QueueUrl = "queue-url",
            ConsumerType = typeof(AsbIntraUpdateConsumer),
        };
}
