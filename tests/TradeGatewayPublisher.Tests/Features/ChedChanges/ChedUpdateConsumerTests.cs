using System.Text.Json;
using Amazon.SQS.Model;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Infrastructure.TracesGateway;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trade.Gateway.Api.Contract.Certificate;
using TradeGatewayPublisher.Config;
using TradeGatewayPublisher.Features.ChedChanges;

namespace TradeGatewayPublisher.Tests.Features.ChedChanges;

public class ChedUpdateConsumerTests
{
    private readonly ITracesGateway _gateway = Substitute.For<ITracesGateway>();
    private readonly ISnsPublisher _sns = Substitute.For<ISnsPublisher>();
    private readonly ChedUpdateConsumer _sut;

    public ChedUpdateConsumerTests()
    {
        var options = Options.Create(
            new TracesUpdatePublisherOptions
            {
                IntraTopicArn = "test-topic",
                IntraInternalTopicArn = "test-internal-topic",
                ChedTopicArn = "test-ched-topic",
                ChedInternalTopicArn = "test-ched-internal-topic",
            }
        );

        _gateway
            .GetChedCertification("1", Arg.Any<CancellationToken>())
            .Returns(
                new DefraUNVTDCHEDProfile()
                {
                    SpecifiedConsignment = new Consignment(),
                    ExchangedDocument = new ExchangedDocument() { Identifier = "CHEDA.GB.2026.1234567" },
                }
            );

        _sut = new ChedUpdateConsumer(_gateway, _sns, options, NullLogger<ChedUpdateConsumer>.Instance);
    }

    [Fact]
    public async Task ConsumeAsync_should_publish_the_certificate_with_a_duplication_id()
    {
        await _sut.ConsumeAsync(CreateContext("1"), CancellationToken.None);

        await _sns.Received(1)
            .PublishAsync(
                "test-ched-topic",
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Is<string>(duplicationId => !string.IsNullOrWhiteSpace(duplicationId)),
                Arg.Any<CancellationToken>()
            );
    }

    private static MessageContext CreateContext(string id) =>
        new()
        {
            Message = new Message { Body = JsonSerializer.Serialize(new { Id = id, Timestamp = DateTime.UtcNow }) },
            QueueUrl = "queue-url",
            ConsumerType = typeof(ChedUpdateConsumer),
        };
}
