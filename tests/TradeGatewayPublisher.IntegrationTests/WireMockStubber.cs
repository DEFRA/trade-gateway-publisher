using System.Net.Http.Json;
using System.Text.Json;

using Trade.Gateway.Api.Contract.Certificate;
using Trade.Gateway.Api.Contract.ReferenceData;

namespace TradeGatewayPublisher.IntegrationTests;

internal static class WireMockStubber
{
    private static class MappingIds
    {
        public static string IntraFindUpdates => nameof(IntraFindUpdates);
        public static string IntraGetCertification => nameof(IntraGetCertification);

        public static string ChedFindUpdates => nameof(ChedFindUpdates);
        public static string ChedGetCertification => nameof(ChedGetCertification);

        public static string ClassificationTreeNodeFindUpdates => nameof(ClassificationTreeNodeFindUpdates);

        public static string ClassificationTreeNodeDetail => nameof(ClassificationTreeNodeDetail);
    }

    private sealed record WireMockMappingsResponse(List<WireMockMapping>? Mappings);

    private sealed record WireMockMapping(string? Id);

    public static async Task StubIntrasAsync(
        string wireMockBaseUrl,
        string intraId,
        CancellationToken cancellationToken = default
    )
    {
        using var http = new HttpClient();
        http.BaseAddress = new Uri(wireMockBaseUrl);

        // Use a fresh updated timestamp per stub so the polling job treats this as a new item on every run
        var updated = DateTimeOffset.UtcNow;
        var certificationBody = JsonSerializer.Serialize(
            new DefraUNVTDINTRAProfile()
            {
                SpecifiedConsignment = new Consignment(),
                ExchangedDocument = new ExchangedDocument() { Identifier = intraId },
            }
        );

        await StubClassificationTreeNodeUpdatesReturnsEmptyCollection(http, cancellationToken);

        await StubChedUpdatesReturnsEmptyCollection(http, cancellationToken);

        await PostMappingAsync(
            http,
            MappingIds.IntraFindUpdates,
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
            MappingIds.IntraGetCertification,
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
        using var http = new HttpClient();
        http.BaseAddress = new Uri(wireMockBaseUrl);

        // Use a fresh updated timestamp per stub so the polling job treats this as a new item on every run
        var updated = DateTimeOffset.UtcNow;
        var certificationBody = JsonSerializer.Serialize(
            new DefraUNVTDCHEDProfile()
            {
                SpecifiedConsignment = new Consignment(),
                ExchangedDocument = new ExchangedDocument() { Identifier = chedId },
            }
        );

        await StubClassificationTreeNodeUpdatesReturnsEmptyCollection(http, cancellationToken);

        await StubIntraUpdatesReturnsEmptyCollection(http, cancellationToken);

        await PostMappingAsync(
            http,
            MappingIds.ChedFindUpdates,
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
                    }
                },
            },
            cancellationToken
        );

        await PostMappingAsync(
            http,
            MappingIds.ChedGetCertification,
            new
            {
                priority = 1,
                request = new { method = "GET", urlPath = $"/certificates/cheds/{chedId}" },
                response = new { status = 200, body = certificationBody },
            },
            cancellationToken
        );
    }

    public static async Task StubClassificationTreeNodesAsync(
        string wireMockBaseUrl,
        string nodeId,
        string treeId,
        CancellationToken cancellationToken = default
    )
    {
        using var http = new HttpClient();
        http.BaseAddress = new Uri(wireMockBaseUrl);

        // Use a fresh updated timestamp per stub so the polling job treats this as a new item on every run
        var retrievedAt = DateTimeOffset.UtcNow;

        var nodeDetailResponse = new DefraUNVTDProfileClassificationTreeNodeDetailResponse
        {
            TreeId = treeId,
            NodeId = nodeId,
            NodePath = "/root/a",
            RetrievedAt = retrievedAt,
            Node = new DefraUNVTDProfileClassificationTreeNodeDetailResponseNode
            {
                Selectable = true, 
                CnCode = "12345", 
                Label = "Mules",
                NodeType = "nomenclature",
            }
        };

        await StubChedUpdatesReturnsEmptyCollection(http, cancellationToken);

        await StubIntraUpdatesReturnsEmptyCollection(http, cancellationToken);

        await PostMappingAsync(
            http,
            MappingIds.ClassificationTreeNodeFindUpdates,
            new
            {
                priority = 1,
                request = new { method = "GET", urlPathTemplate = "/reference-data/classifications/trees/{treeId}/nodes" },
                response = new
                {
                    status = 200,
                    jsonBody = new DefraUNVTDProfileClassificationTreeResponse {
                                Nodes = [
                                    new ClassificationTreeNode
                                    {
                                        NodeId = nodeDetailResponse.NodeId,
                                        Path = nodeDetailResponse.NodePath,
                                        Label = nodeDetailResponse.Node.Label,
                                        Selectable = nodeDetailResponse.Node.Selectable,
                                        NodeType = nodeDetailResponse.Node.NodeType,
                                        CnCode = nodeDetailResponse.Node.CnCode,
                                    }
                                ],
                                RetrievedAt = retrievedAt
                            }
                },
            },
            cancellationToken
        );

        await PostMappingAsync(
            http,
            MappingIds.ClassificationTreeNodeDetail,
            new
            {
                priority = 1,
                request = new { method = "GET", urlPathTemplate = "/reference-data/classifications/trees/{treeId}/nodes/{nodeId}" },
                response = new
                {
                    status = 200,
                    jsonBody = nodeDetailResponse
                },
            },
            cancellationToken
        );
    }

    private static async Task StubIntraUpdatesReturnsEmptyCollection(HttpClient http, CancellationToken cancellationToken)
    {
        await PostMappingAsync(
            http,
            MappingIds.IntraFindUpdates,
            new
            {
                priority = 1,
                request = new { method = "GET", urlPath = "/certificates/intras" },
                response = new
                {
                    status = 200,
                    jsonBody = new DefraUNVTDINTRASummaryProfile
                        {
                            HasMore = false,
                            Offset = 0,
                            PageSize = 25,
                            Items = []
                        }
                },
            },
            cancellationToken
        );
    }

    private static async Task StubChedUpdatesReturnsEmptyCollection(HttpClient http, CancellationToken cancellationToken)
    {
        await PostMappingAsync(
            http,
            MappingIds.ChedFindUpdates,
            new
            {
                priority = 1,
                request = new { method = "GET", urlPath = "/certificates/cheds" },
                response = new
                {
                    status = 200,
                    jsonBody =
                            new DefraUNVTDCHEDSummaryProfile
                            {
                                HasMore = false,
                                Offset = 0,
                                PageSize = 25,
                                Items = []
                            }
                },
            },
            cancellationToken
        );
    }

    private static async Task StubClassificationTreeNodeUpdatesReturnsEmptyCollection(HttpClient http, CancellationToken cancellationToken)
    {
        await PostMappingAsync(
            http,
            MappingIds.ClassificationTreeNodeFindUpdates,
            new
            {
                priority = 1,
                request = new { method = "GET", urlPathTemplate = "/reference-data/classifications/trees/{treeId}/nodes" },
                response = new
                {
                    status = 200,
                    jsonBody = new DefraUNVTDProfileClassificationTreeResponse
                    {
                        Nodes = [],
                        RetrievedAt = new DateTimeOffset(2026, 10,1,0,0,0, TimeSpan.Zero)
                    },
                },
            },
            cancellationToken
        );
    }

    public static async Task ResetAsync(string wireMockBaseUrl, CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient();
        http.BaseAddress = new Uri(wireMockBaseUrl);

        var mappingsResponse = await http.GetFromJsonAsync<WireMockMappingsResponse>("/__admin/mappings", cancellationToken);

        foreach (var id in mappingsResponse?.Mappings?.Select(m => m.Id).Where(id => !string.IsNullOrWhiteSpace(id)) ?? [])
        {
            await http.DeleteAsync($"/__admin/mappings/{id}", cancellationToken);
        }

        // clear out previous requests
        await http.DeleteAsync("/__admin/requests", cancellationToken);
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
