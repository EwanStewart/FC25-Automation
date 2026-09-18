using Automation.Flow;

namespace Automation.Trading;

public enum BidOutcome
{
    Registered,
    Overtaken,
    Failed
}

public readonly record struct TargetFacts(
    string Classes,
    uint? MinutesLeft,
    uint? MinimumBid,
    uint Estimate,
    int? SecondsLeft = null,
    string? BidState = null);

public static class Snipe
{
    private const string HIGHEST_STATE = "highest";
    private const string OUTBID_STATE = "outbid";
    private const string ACTIVE_STATE = "active";

    public static bool IsLive(string classes)
    {
        var tokens = classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        return !tokens.Contains("expired") && !tokens.Contains("won");
    }

    public static bool IsLive(string classes, string? tradeState)
    {
        return IsLive(classes) && (tradeState == null || tradeState == ACTIVE_STATE);
    }

    public static bool IsOurs(TargetFacts facts)
    {
        return BidRow.IsOurs(facts.Classes) || facts.BidState == HIGHEST_STATE;
    }

    public static bool ShouldBid(TargetFacts facts, uint marginCoins, uint maxBid, int aimSeconds)
    {
        var due = facts.SecondsLeft.HasValue ? facts.SecondsLeft.Value <= aimSeconds : facts.MinutesLeft is 0;
        var open = IsLive(facts.Classes) && !IsOurs(facts);

        return open && due && facts.MinimumBid.HasValue &&
               facts.MinimumBid.Value <= Ceiling(facts.Estimate, marginCoins, maxBid);
    }

    public static bool ShouldWatch(uint estimate, uint minimumBid, uint marginCoins, uint maxBid, int watched,
        int batchSize)
    {
        return watched < batchSize && minimumBid <= Ceiling(estimate, marginCoins, maxBid);
    }

    public static bool WatchConfirmed(bool unwatchEnabled, int? responseStatus)
    {
        return unwatchEnabled && (responseStatus == null || responseStatus == 200);
    }

    public static bool BatchFinished(IEnumerable<string> rowClasses)
    {
        return !rowClasses.Any(IsLive);
    }

    public static BidOutcome Outcome(string classes, uint ourAmount, uint? rowBidAfter, string? bidState = null)
    {
        var result = BidOutcome.Failed;
        var overtaken = rowBidAfter.HasValue && rowBidAfter.Value > ourAmount;

        if (BidRow.IsOurs(classes) || bidState == HIGHEST_STATE) result = BidOutcome.Registered;
        else if (overtaken) result = BidOutcome.Overtaken;

        return result;
    }

    public static uint Ceiling(uint estimate, uint marginCoins, uint maxBid)
    {
        return Math.Min(Pricing.MaxBid(estimate, marginCoins), maxBid);
    }
}
