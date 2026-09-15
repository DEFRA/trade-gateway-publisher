using System.Net;
using Infrastructure;
using Infrastructure.Messaging.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Certificate;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static void MapEndpoints(this IEndpointRouteBuilder app)
    {
        const string groupName = "Jobs";

        app.MapGet("ched/{chedId}", PublishChed)
            .WithName("ForceChedPublish")
            .WithTags(groupName)
            .WithSummary("Forces a publish of a ched")
            .WithDescription("Forces a publish of a ched")
            .ProducesProblem(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    /// <param name="chedId"></param>
    /// <param name="gatewayChedClient"></param>
    /// <param name="snsPublisher"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    private static async Task<IResult> PublishChed(
        [FromRoute] string chedId,
        [FromServices] ITracesGatewayChedClient gatewayChedClient,
        [FromServices] ISnsPublisher snsPublisher,
        [FromServices] IOptions<TracesUpdatePublisherOptions> options,
        CancellationToken cancellationToken
    )
    {
        var apiResponse = await gatewayChedClient.GetChedCertification(chedId, cancellationToken);
        if (apiResponse.StatusCode == HttpStatusCode.NotFound)
        {
            return Results.NotFound();
        }
        await apiResponse.EnsureSuccessfulAsync();
        await snsPublisher.PublishAsync(
            options.Value.ChedInternalTopicArn,
            new DefraUNVTDCHEDSummaryProfileItem()
            {
                Id = chedId,
                Origin = "Force",
                Created = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow,
            }.ToJson(),
            cancellationToken: cancellationToken,
            duplicationId: apiResponse.GetDuplicationId()
        );

        return Results.Ok();
    }
}
