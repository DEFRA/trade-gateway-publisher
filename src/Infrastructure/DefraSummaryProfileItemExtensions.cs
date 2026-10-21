using Refit;
using Trade.Gateway.Api.Contract.Certificate;
using Trade.Gateway.Api.Contract.ReferenceData;

namespace Infrastructure;

public static class DefraSummaryProfileItemExtensions
{
    public static string GetDuplicationId(this DefraUNVTDINTRASummaryProfileItem item) =>
        $"{item.Id}_{item.Updated.ToUnixTimeMilliseconds()}";

    public static string GetDuplicationId(this DefraUNVTDCHEDSummaryProfileItem item) =>
        $"{item.Id}_{item.Updated.ToUnixTimeMilliseconds()}";

    public static string GetDuplicationId(this ApiResponse<DefraUNVTDCHEDProfile> item) =>
        $"{item.Content?.ExchangedDocument?.Identifier}_{item.Content?.LastUpdated?.ToUnixTimeMilliseconds()}";

    public static string GetDuplicationId(this ClassificationTreeNode item, DateTimeOffset retrievalDateTime) =>
        $"{item.NodeId}_{retrievalDateTime.ToUnixTimeMilliseconds()}";
}
