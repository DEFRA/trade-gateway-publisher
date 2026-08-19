using System.Net;
using System.Text.Json;
using Amazon.SQS.Model;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Refit;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Certificate;
using TradeGatewayPublisher.Config;
using TradeGatewayPublisher.Features.ChedChanges;

namespace TradeGatewayPublisher.Tests.Features.ChedChanges;

public class ChedUpdateConsumerTests
{
    private readonly ITracesGatewayChedClient _gateway = Substitute.For<ITracesGatewayChedClient>();
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

        var response = new ApiResponse<DefraUNVTDCHEDProfile>(
            new HttpResponseMessage(HttpStatusCode.OK),
            new DefraUNVTDCHEDProfile()
            {
                SpecifiedConsignment = new Consignment(),
                ExchangedDocument = new ExchangedDocument() { Identifier = "CHEDA.GB.2026.1234567" },
            },
            new RefitSettings()
        );

        _gateway.GetChedCertification("1", Arg.Any<CancellationToken>()).Returns(response);

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
            Message = new Message
            {
                Body = JsonSerializer.Serialize(
                    new DefraUNVTDCHEDSummaryProfileItem
                    {
                        Id = id,
                        Origin = "Origin",
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow,
                    }
                ),
            },
            QueueUrl = "queue-url",
            ConsumerType = typeof(ChedUpdateConsumer),
        };
}
