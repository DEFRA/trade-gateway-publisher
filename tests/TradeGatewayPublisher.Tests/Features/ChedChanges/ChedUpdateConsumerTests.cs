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

    [Fact]
    public async Task ConsumeAsync_should_publish_linked_cheds_when_partially_rejected()
    {
        _gateway
            .GetChedCertification("2", Arg.Any<CancellationToken>())
            .Returns(
                CreateResponse(
                    new ExchangedDocument
                    {
                        Identifier = "CHEDPP.XI.2026.0000160",
                        DocumentStatusCode = "237",
                        ReferenceDocument =
                        [
                            new ReferencedDocument
                            {
                                TypeCode = "544",
                                RelationshipTypeCode = "628",
                                Identifier = "CHEDPP.XI.2026.0000160",
                            },
                            new ReferencedDocument
                            {
                                TypeCode = "662",
                                RelationshipTypeCode = "813",
                                Identifier = "123",
                            },
                            new ReferencedDocument
                            {
                                TypeCode = "75",
                                RelationshipTypeCode = "57",
                                Identifier = "CHEDPP.XI.2026.0000160R",
                            },
                            new ReferencedDocument
                            {
                                TypeCode = "75",
                                RelationshipTypeCode = "57",
                                Identifier = "CHEDPP.XI.2026.0000160V",
                            },
                        ],
                    }
                )
            );
        _gateway
            .GetChedCertification("CHEDPP.XI.2026.0000160R", Arg.Any<CancellationToken>())
            .Returns(CreateResponse(new ExchangedDocument { Identifier = "CHEDPP.XI.2026.0000160R" }));
        _gateway
            .GetChedCertification("CHEDPP.XI.2026.0000160V", Arg.Any<CancellationToken>())
            .Returns(CreateResponse(new ExchangedDocument { Identifier = "CHEDPP.XI.2026.0000160V" }));

        await _sut.ConsumeAsync(CreateContext("2"), CancellationToken.None);

        await _gateway.Received(1).GetChedCertification("CHEDPP.XI.2026.0000160R", Arg.Any<CancellationToken>());
        await _gateway.Received(1).GetChedCertification("CHEDPP.XI.2026.0000160V", Arg.Any<CancellationToken>());
        await _gateway.DidNotReceive().GetChedCertification("123", Arg.Any<CancellationToken>());
        await _sns.Received(3)
            .PublishAsync(
                "test-ched-topic",
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
        await _sns.Received(1)
            .PublishAsync(
                "test-ched-topic",
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Is<string>(duplicationId => duplicationId.StartsWith("CHEDPP.XI.2026.0000160R_")),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ConsumeAsync_should_not_publish_linked_cheds_when_not_partially_rejected()
    {
        _gateway
            .GetChedCertification("3", Arg.Any<CancellationToken>())
            .Returns(
                CreateResponse(
                    new ExchangedDocument
                    {
                        Identifier = "CHEDPP.XI.2026.0000161",
                        DocumentStatusCode = "70",
                        ReferenceDocument =
                        [
                            new ReferencedDocument
                            {
                                TypeCode = "75",
                                RelationshipTypeCode = "57",
                                Identifier = "CHEDPP.XI.2026.0000161R",
                            },
                        ],
                    }
                )
            );

        await _sut.ConsumeAsync(CreateContext("3"), CancellationToken.None);

        await _gateway.DidNotReceive().GetChedCertification("CHEDPP.XI.2026.0000161R", Arg.Any<CancellationToken>());
        await _sns.Received(1)
            .PublishAsync(
                "test-ched-topic",
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    private static ApiResponse<DefraUNVTDCHEDProfile> CreateResponse(ExchangedDocument document) =>
        new(
            new HttpResponseMessage(HttpStatusCode.OK),
            new DefraUNVTDCHEDProfile
            {
                SpecifiedConsignment = new Consignment(),
                ExchangedDocument = document,
                LastUpdated = DateTimeOffset.UtcNow,
            },
            new RefitSettings()
        );

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
