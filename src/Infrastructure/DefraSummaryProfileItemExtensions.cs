using Trade.Gateway.Api.Contract.Certificate;

namespace Infrastructure;

public static class DefraSummaryProfileItemExtensions
{
    public static string GetDuplicationId(this DefraUNVTDINTRASummaryProfileItem item) =>
        $"{item.Id}_{item.Updated.ToUnixTimeMilliseconds()}";

    public static string GetDuplicationId(this DefraUNVTDCHEDSummaryProfileItem item) =>
        $"{item.Id}_{item.Updated.ToUnixTimeMilliseconds()}";
}