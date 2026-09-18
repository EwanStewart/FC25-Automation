using System.Text.Json;

namespace Automation.Trading;

public enum CaptureKind
{
    Search,
    TradeStatus,
    Bid,
    Watchlist,
    Other
}

public sealed record AuctionListing(
    string TradeId,
    string TradeState,
    string BidState,
    int Expires,
    uint CurrentBid,
    uint StartingBid,
    uint BuyNowPrice,
    int Rating,
    string Position,
    uint LastSalePrice,
    uint MarketAverage);

public sealed record BidResponse(BidOutcome Outcome, string Reason);

public static class UtasPayloads
{
    private const string HIGHEST_STATE = "highest";

    public static IReadOnlyList<AuctionListing> ParseAuctions(string json)
    {
        List<AuctionListing> result = [];

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("auctionInfo", out var auctions) &&
                auctions.ValueKind == JsonValueKind.Array)
                result = auctions.EnumerateArray().Select(ParseAuction).ToList();
        }
        catch (JsonException)
        {
        }

        return result;
    }

    public static IReadOnlyList<uint> Asks(string json)
    {
        return ParseAuctions(json).Where(auction => auction.BuyNowPrice > 0).Select(auction => auction.BuyNowPrice)
            .ToList();
    }

    public static CaptureKind Classify(string method, string url)
    {
        var result = CaptureKind.Other;
        var utas = url.Contains("/ut/game/", StringComparison.Ordinal);
        var put = method.Equals("PUT", StringComparison.OrdinalIgnoreCase);

        if (utas && url.Contains("/transfermarket", StringComparison.Ordinal)) result = CaptureKind.Search;
        else if (utas && url.Contains("/trade/status", StringComparison.Ordinal)) result = CaptureKind.TradeStatus;
        else if (utas && put && (url.Contains("/bid", StringComparison.Ordinal) || url.Contains("/auctionhouse", StringComparison.Ordinal)))
            result = CaptureKind.Bid;
        else if (utas && url.Contains("/watchlist", StringComparison.Ordinal)) result = CaptureKind.Watchlist;

        return result;
    }

    public static BidResponse? BidResult(int status, string json, string tradeId)
    {
        BidResponse? result = null;
        var auction = ParseAuctions(json).FirstOrDefault(entry => entry.TradeId == tradeId);

        if (status != 200) result = new BidResponse(BidOutcome.Failed, FailureReason(status, json));
        else if (auction != null)
            result = new BidResponse(auction.BidState == HIGHEST_STATE ? BidOutcome.Registered : BidOutcome.Overtaken,
                auction.BidState);

        return result;
    }

    private static string FailureReason(int status, string json)
    {
        var reason = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("reason", out var element))
                reason = element.GetString() ?? string.Empty;
        }
        catch (JsonException)
        {
        }

        return $"{status} {reason}".Trim();
    }

    private static AuctionListing ParseAuction(JsonElement element)
    {
        var item = element.TryGetProperty("itemData", out var data) ? data : default;

        return new AuctionListing(
            Text(element, "tradeId"),
            Text(element, "tradeState"),
            Text(element, "bidState"),
            Number(element, "expires"),
            (uint)Number(element, "currentBid"),
            (uint)Number(element, "startingBid"),
            (uint)Number(element, "buyNowPrice"),
            Number(item, "rating"),
            Text(item, "preferredPosition"),
            (uint)Number(item, "lastSalePrice"),
            (uint)Number(item, "marketAverage"));
    }

    private static string Text(JsonElement element, string name)
    {
        var result = string.Empty;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value))
            result = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();

        return result;
    }

    private static int Number(JsonElement element, string name)
    {
        var result = 0;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed))
            result = (int)Math.Max(0, Math.Min(int.MaxValue, parsed));

        return result;
    }
}
