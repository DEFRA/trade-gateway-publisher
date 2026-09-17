using System.Net;
using AwesomeAssertions;
using Infrastructure.Messaging.Publishing;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using NSubstitute;
using Refit;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Certificate;
using TradeGatewayPublisher.Config;
using TradeGatewayPublisher.Endpoints;

namespace TradeGatewayPublisher.Tests.Endpoints;

public class EndpointRouteBuilderExtensionsTests
{
    private readonly ITracesGatewayChedClient _gateway = Substitute.For<ITracesGatewayChedClient>();
    private readonly ISnsPublisher _sns = Substitute.For<ISnsPublisher>();
    private readonly IOptions<TracesUpdatePublisherOptions> _options = Options.Create(
        new TracesUpdatePublisherOptions
        {
            IntraTopicArn = "test-intra-topic",
            IntraInternalTopicArn = "test-intra-internal-topic",
            ChedTopicArn = "test-ched-topic",
            ChedInternalTopicArn = "test-ched-internal-topic",
        }
    );

    [Fact]
    public async Task PublishChed_WhenCertificateExists_ReturnsOkAndPublishesToSns()
    {
        var response = new ApiResponse<DefraUNVTDCHEDProfile>(
            new HttpResponseMessage(HttpStatusCode.OK),
            new DefraUNVTDCHEDProfile
            {
                SpecifiedConsignment = new Consignment(),
                ExchangedDocument = new ExchangedDocument { Identifier = "CHEDA.GB.2026.1234567" },
            },
            new RefitSettings()
        );
        _gateway.GetChedCertification("1", Arg.Any<CancellationToken>()).Returns(response);

        var result = await EndpointRouteBuilderExtensions.PublishChed(
            "1",
            _gateway,
            _sns,
            _options,
            CancellationToken.None
        );

        result.Should().BeOfType<Ok>();
        await _sns.Received(1)
            .PublishAsync(
                "test-ched-internal-topic",
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Is<string>(duplicationId => !string.IsNullOrWhiteSpace(duplicationId)),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PublishChed_WhenCertificateDoesNotExist_ReturnsNotFoundAndDoesNotPublish()
    {
        var response = new ApiResponse<DefraUNVTDCHEDProfile>(
            new HttpResponseMessage(HttpStatusCode.NotFound),
            null,
            new RefitSettings()
        );
        _gateway.GetChedCertification("missing", Arg.Any<CancellationToken>()).Returns(response);

        var result = await EndpointRouteBuilderExtensions.PublishChed(
            "missing",
            _gateway,
            _sns,
            _options,
            CancellationToken.None
        );

        result.Should().BeOfType<NotFound>();
        await _sns.DidNotReceive()
            .PublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }
}
