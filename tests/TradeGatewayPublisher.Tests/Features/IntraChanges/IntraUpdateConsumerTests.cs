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
using TradeGatewayPublisher.Features.IntraChanges;

namespace TradeGatewayPublisher.Tests.Features.IntraChanges;

public class IntraUpdateConsumerTests
{
    private readonly ITracesGatewayIntraClient _gateway = Substitute.For<ITracesGatewayIntraClient>();
    private readonly ISnsPublisher _sns = Substitute.For<ISnsPublisher>();
    private readonly IntraUpdateConsumer _sut;

    public IntraUpdateConsumerTests()
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

        var response = new ApiResponse<DefraUNVTDINTRAProfile>(
            new HttpResponseMessage(HttpStatusCode.OK),
            new DefraUNVTDINTRAProfile()
            {
                SpecifiedConsignment = new Consignment(),
                ExchangedDocument = new ExchangedDocument() { Identifier = "CHEDA.GB.2026.1234567" },
            },
            new RefitSettings()
        );

        _gateway.GetIntraCertification("1", Arg.Any<CancellationToken>()).Returns(response);

        _sut = new IntraUpdateConsumer(_gateway, _sns, options, NullLogger<IntraUpdateConsumer>.Instance);
    }

    [Fact]
    public async Task ConsumeAsync_should_publish_the_certificate_with_a_duplication_id()
    {
        await _sut.ConsumeAsync(CreateContext("1"), CancellationToken.None);

        await _sns.Received(1)
            .PublishAsync(
                "test-topic",
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
                    new DefraUNVTDINTRASummaryProfileItem
                    {
                        Id = id,
                        Origin = "Origin",
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow,
                    }
                ),
            },
            QueueUrl = "queue-url",
            ConsumerType = typeof(IntraUpdateConsumer),
        };
}
