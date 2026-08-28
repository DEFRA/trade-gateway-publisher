using System.Net.Http.Json;
using System.Reflection.Metadata;
using System.Text.Json;
using Trade.Gateway.Api.Contract.Certificate;

namespace TradeGatewayPublisher.IntegrationTests.IntraChanges;

internal static class WireMockStubber
{
    private static readonly string[] s_mappingIds = ["intra-find-updates", "intra-get-certification"];

    public static async Task StubAsync(
        string wireMockBaseUrl,
        string intraId,
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
                ExchangedDocument = new ExchangedDocument() { Identifier = intraId },
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
                    jsonBody = new
                    {
                        items = new List<DefraUNVTDINTRASummaryProfileItem>
                        {
                            new DefraUNVTDINTRASummaryProfileItem
                            {
                                Id = intraId,
                                Updated = updated,
                                Created = DateTimeOffset.UtcNow,
                                Origin = "Int-Test",
                            }
                        }
                    }
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
