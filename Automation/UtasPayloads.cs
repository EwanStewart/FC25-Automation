using System.Text.Json;

namespace Automation.Trading;

public enum CaptureKind
{
    Search,
    TradeStatus,
    Bid,
    Watch,
    Unwatch,
    Watchlist,
    Club,
    ActiveSquad,
    SbcSets,
    SbcChallenges,
    SbcSquad,
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
    uint MarketAverage,
    long ItemId = 0,
    int AssetId = 0,
    int TeamId = 0,
    int LeagueId = 0,
    int NationId = 0,
    int RareFlag = 0,
    IReadOnlyList<string>? PossiblePositions = null);

public sealed record BidResponse(BidOutcome Outcome, string Reason);

public sealed record BuyResponse(bool Bought, string Reason);

public static class UtasPayloads
{
    private const string HIGHEST_STATE = "highest";
    private const string BOUGHT_STATE = "buyNow";

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
        var get = utas && method.Equals("GET", StringComparison.OrdinalIgnoreCase);
        var put = utas && method.Equals("PUT", StringComparison.OrdinalIgnoreCase);
        var delete = utas && method.Equals("DELETE", StringComparison.OrdinalIgnoreCase);
        var post = utas && method.Equals("POST", StringComparison.OrdinalIgnoreCase);

        if (get && url.Contains("/transfermarket", StringComparison.Ordinal)) result = CaptureKind.Search;
        else if (get && url.Contains("/trade/status", StringComparison.Ordinal)) result = CaptureKind.TradeStatus;
        else if (put && (url.Contains("/bid", StringComparison.Ordinal) || url.Contains("/auctionhouse", StringComparison.Ordinal)))
            result = CaptureKind.Bid;
        else if (put && url.Contains("/watchlist", StringComparison.Ordinal)) result = CaptureKind.Watch;
        else if (delete && url.Contains("/watchlist", StringComparison.Ordinal)) result = CaptureKind.Unwatch;
        else if (get && url.Contains("/watchlist", StringComparison.Ordinal)) result = CaptureKind.Watchlist;
        else if (post && url.EndsWith("/club", StringComparison.Ordinal)) result = CaptureKind.Club;
        else if (get && url.Contains("/squad/active", StringComparison.Ordinal)) result = CaptureKind.ActiveSquad;
        else if (get && url.EndsWith("/sbs/sets", StringComparison.Ordinal)) result = CaptureKind.SbcSets;
        else if (post && url.Contains("/sbs/challenge/", StringComparison.Ordinal))
            result = CaptureKind.SbcSquad;
        else if (get && url.Contains("/sbs/challenge/", StringComparison.Ordinal) &&
                 url.EndsWith("/squad", StringComparison.Ordinal)) result = CaptureKind.SbcSquad;
        else if (get && url.Contains("/sbs/setId/", StringComparison.Ordinal) &&
                 url.EndsWith("/challenges", StringComparison.Ordinal)) result = CaptureKind.SbcChallenges;

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

    public static BuyResponse? BuyResult(int status, string json, string tradeId)
    {
        BuyResponse? result = null;
        var auction = ParseAuctions(json).FirstOrDefault(entry => entry.TradeId == tradeId);

        if (status != 200) result = new BuyResponse(false, FailureReason(status, json));
        else if (auction != null)
            result = new BuyResponse(auction.BidState == BOUGHT_STATE || auction.BidState == HIGHEST_STATE,
                $"{auction.TradeState} {auction.BidState}");

        return result;
    }

    private static string FailureReason(int status, string json)
    {
        var reason = json.Length > 120 ? json[..120] : json;

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind == JsonValueKind.Object)
                reason = $"{Text(document.RootElement, "string")} {Text(document.RootElement, "reason")}".Trim();
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
            (uint)Number(item, "marketAverage"),
            Identity(item, "id"),
            Number(item, "assetId"),
            Number(item, "teamid"),
            Number(item, "leagueId"),
            Number(item, "nation"),
            Number(item, "rareflag"),
            Positions(item));
    }

    private static string Text(JsonElement element, string name)
    {
        var result = string.Empty;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value))
            result = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();

        return result;
    }

    private static long Identity(JsonElement element, string name)
    {
        long result = 0;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed)) result = parsed;

        return result;
    }

    private static IReadOnlyList<string> Positions(JsonElement element)
    {
        IReadOnlyList<string> result = [];

        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("possiblePositions", out var value) &&
            value.ValueKind == JsonValueKind.Array)
            result = value.EnumerateArray().Where(entry => entry.ValueKind == JsonValueKind.String)
                .Select(entry => entry.GetString() ?? string.Empty).Where(entry => entry.Length > 0).ToList();

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
