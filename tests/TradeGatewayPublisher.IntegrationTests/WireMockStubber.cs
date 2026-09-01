using System.Net.Http.Json;
using System.Reflection.Metadata;
using System.Text.Json;
using Trade.Gateway.Api.Contract.Certificate;

namespace TradeGatewayPublisher.IntegrationTests;

internal static class WireMockStubber
{
    private static readonly string[] s_mappingIds =
    [
        "intra-find-updates",
        "intra-get-certification",
        "ched-find-updates",
        "ched-get-certification",
    ];

    public static async Task StubIntrasAsync(
        string wireMockBaseUrl,
        string intraId,
        CancellationToken cancellationToken = default
    )
    {
        using var http = new HttpClient { BaseAddress = new Uri(wireMockBaseUrl) };
        // Use a fresh updated timestamp per stub so the polling job treats this as a new item on every run
        var updated = DateTimeOffset.UtcNow;
        var certificationBody = JsonSerializer.Serialize(
            new DefraUNVTDINTRAProfile()
            {
                SpecifiedConsignment = new Consignment(),
                ExchangedDocument = new ExchangedDocument() { Identifier = intraId },
            }
        );

        await PostMappingAsync(
            http,
            "ched-find-updates",
            new
            {
                priority = 1,
                request = new { method = "GET", urlPath = "/certificates/cheds" },
                response = new
                {
                    status = 200,
                    jsonBody = new DefraUNVTDCHEDSummaryProfile
                    {
                        Items = [],
                        HasMore = false,
                        Offset = 0,
                        PageSize = 5,
                    },
                },
            },
            cancellationToken
        );

        await PostMappingAsync(
            http,
            "intra-find-updates",
            new
            {
                priority = 1,
                request = new { method = "GET", urlPath = "/certificates/intras" },
                response = new
                {
                    status = 200,
                    jsonBody = new DefraUNVTDINTRASummaryProfile
                    {
                        Items =
                        [
                            new DefraUNVTDINTRASummaryProfileItem
                            {
                                Id = intraId,
                                Updated = updated,
                                Created = DateTimeOffset.UtcNow,
                                Origin = "Int-Test",
                            },
                        ],
                    },
                },
            },
            cancellationToken
        );

        await PostMappingAsync(
            http,
            "intra-get-certification",
            new
            {
                priority = 1,
                request = new { method = "GET", urlPath = $"/certificates/intras/{intraId}" },
                response = new { status = 200, body = certificationBody },
            },
            cancellationToken
        );
    }

    public static async Task StubChedsAsync(
        string wireMockBaseUrl,
        string chedId,
        CancellationToken cancellationToken = default
    )
    {
        using var http = new HttpClient { BaseAddress = new Uri(wireMockBaseUrl) };
        // Use a fresh updated timestamp per stub so the polling job treats this as a new item on every run
        var updated = DateTimeOffset.UtcNow;
        var certificationBody = JsonSerializer.Serialize(
            new DefraUNVTDCHEDProfile()
            {
                SpecifiedConsignment = new Consignment(),
                ExchangedDocument = new ExchangedDocument() { Identifier = chedId },
            }
        );

        await PostMappingAsync(
            http,
            "intra-find-updates",
            new
            {
                priority = 1,
                request = new { method = "GET", urlPath = "/certificates/intras" },
                response = new
                {
                    status = 200,
                    jsonBody = new { items = new List<DefraUNVTDCHEDSummaryProfileItem> { } },
                },
            },
            cancellationToken
        );

        await PostMappingAsync(
            http,
            "ched-find-updates",
            new
            {
                priority = 1,
                request = new { method = "GET", urlPath = "/certificates/cheds" },
                response = new
                {
                    status = 200,
                    jsonBody = new DefraUNVTDCHEDSummaryProfile
                    {
                        Items =
                        [
                            new DefraUNVTDCHEDSummaryProfileItem
                            {
                                Id = chedId,
                                Updated = updated,
                                Created = DateTimeOffset.UtcNow,
                                Origin = "Int-Test",
                            },
                        ],
                        HasMore = false,
                        Offset = 0,
                        PageSize = 5,
                    },
                },
            },
            cancellationToken
        );

        await PostMappingAsync(
            http,
            "ched-get-certification",
            new
            {
                priority = 1,
                request = new { method = "GET", urlPath = $"/certificates/cheds/{chedId}" },
                response = new { status = 200, body = certificationBody },
            },
            cancellationToken
        );
    }

    public static async Task ResetAsync(string wireMockBaseUrl, CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { BaseAddress = new Uri(wireMockBaseUrl) };
        foreach (var id in s_mappingIds)
        {
            await http.DeleteAsync($"/__admin/mappings/{id}", cancellationToken);
        }
    }

    private static async Task PostMappingAsync(
        HttpClient http,
        string id,
        object mapping,
        CancellationToken cancellationToken
    )
    {
        var response = await http.PostAsJsonAsync($"/__admin/mappings?id={id}", mapping, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
