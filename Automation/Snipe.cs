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
    string? BidState = null,
    bool Frozen = false);

public static class Snipe
{
    private const string HIGHEST_STATE = "highest";
    private const string OUTBID_STATE = "outbid";
    private const string ACTIVE_STATE = "active";
    private const int FAST_TIER_SECONDS = 60;
    private static readonly (int secondsLeft, int refreshMs)[] REFRESH_TIERS = { (30, 1000), (60, 5000), (600, 120000) };
    private const int SLOWEST_REFRESH_MS = 600000;

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

    public static int RefreshIntervalMs(int secondsLeft)
    {
        var tier = REFRESH_TIERS.FirstOrDefault(entry => secondsLeft < entry.secondsLeft);

        return tier == default ? SLOWEST_REFRESH_MS : tier.refreshMs;
    }

    public static bool IsFrozen(int? ageMs, int? secondsLeft)
    {
        var result = false;

        if (ageMs.HasValue && secondsLeft.HasValue && secondsLeft.Value <= FAST_TIER_SECONDS)
        {
            var secondsAtLastUpdate = secondsLeft.Value + ageMs.Value / 1000;
            result = ageMs.Value > 2 * RefreshIntervalMs(secondsAtLastUpdate);
        }

        return result;
    }

    public static bool StatusFreezesRows(int status)
    {
        return status != 200 && status != 401;
    }

    public static bool IsSacrificial(TargetFacts facts, uint marginCoins, uint maxBid)
    {
        return IsLive(facts.Classes) && !IsOurs(facts) && facts.MinimumBid.HasValue &&
               facts.MinimumBid.Value > Ceiling(facts.Estimate, marginCoins, maxBid);
    }

    public static bool WatchConfirmed(bool unwatchEnabled, int? responseStatus, bool responseRequired)
    {
        var accepted = responseStatus == 200 || (responseStatus == null && !responseRequired);

        return unwatchEnabled && accepted;
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
