using System.Net;
using AwesomeAssertions;
using Refit;
using Trade.Gateway.Api.Contract.Certificate;

namespace Infrastructure.Tests;

public class DefraSummaryProfileItemExtensionsTests
{
    // 2026-01-15T10:30:00Z
    private static readonly DateTimeOffset Updated = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
    private const long UpdatedUnixMilliseconds = 1768473000000;

    [Fact]
    public void GetDuplicationId_WithIntraSummaryProfileItem_ReturnsIdAndUnixTimestamp()
    {
        var item = new DefraUNVTDINTRASummaryProfileItem
        {
            Id = "intra-1",
            Origin = "Origin",
            Created = Updated,
            Updated = Updated,
        };

        var result = item.GetDuplicationId();

        result.Should().Be($"intra-1_{UpdatedUnixMilliseconds}");
    }

    [Fact]
    public void GetDuplicationId_WithChedSummaryProfileItem_ReturnsIdAndUnixTimestamp()
    {
        var item = new DefraUNVTDCHEDSummaryProfileItem
        {
            Id = "ched-1",
            Origin = "Origin",
            Created = Updated,
            Updated = Updated,
        };

        var result = item.GetDuplicationId();

        result.Should().Be($"ched-1_{UpdatedUnixMilliseconds}");
    }

    [Fact]
    public void GetDuplicationId_WithApiResponseOfChedProfile_ReturnsIdentifierAndUnixTimestamp()
    {
        var response = new ApiResponse<DefraUNVTDCHEDProfile>(
            new HttpResponseMessage(HttpStatusCode.OK),
            new DefraUNVTDCHEDProfile
            {
                SpecifiedConsignment = new Consignment(),
                ExchangedDocument = new ExchangedDocument { Identifier = "CHEDA.GB.2026.1234567" },
                LastUpdated = Updated,
            },
            new RefitSettings()
        );

        var result = response.GetDuplicationId();

        result.Should().Be($"CHEDA.GB.2026.1234567_{UpdatedUnixMilliseconds}");
    }

    [Fact]
    public void GetDuplicationId_WithApiResponseOfChedProfile_WhenContentIsNull_ReturnsUnderscoreSeparator()
    {
        var response = new ApiResponse<DefraUNVTDCHEDProfile>(
            new HttpResponseMessage(HttpStatusCode.NotFound),
            null,
            new RefitSettings()
        );

        var result = response.GetDuplicationId();

        result.Should().Be("_");
    }
}
