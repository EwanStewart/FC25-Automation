using Automation.Flow;

namespace Automation.Trading;

public enum BidOutcome
{
    Registered,
    Overtaken,
    Failed
}

public readonly record struct TargetFacts(string Classes, uint? MinutesLeft, uint? MinimumBid, uint Estimate);

public static class Snipe
{
    public static bool IsLive(string classes)
    {
        var tokens = classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        return !tokens.Contains("expired") && !tokens.Contains("won");
    }

    public static bool ShouldBid(TargetFacts facts, uint marginCoins, uint maxBid)
    {
        var underAMinute = facts.MinutesLeft is 0;
        var open = IsLive(facts.Classes) && !BidRow.IsOurs(facts.Classes);

        return open && underAMinute && facts.MinimumBid.HasValue &&
               facts.MinimumBid.Value <= Ceiling(facts.Estimate, marginCoins, maxBid);
    }

    public static bool ShouldWatch(uint estimate, uint minimumBid, uint marginCoins, uint maxBid, int watched,
        int batchSize)
    {
        return watched < batchSize && minimumBid <= Ceiling(estimate, marginCoins, maxBid);
    }

    public static bool BatchFinished(IEnumerable<string> rowClasses)
    {
        return !rowClasses.Any(IsLive);
    }

    public static BidOutcome Outcome(string classes, uint ourAmount, uint? rowBidAfter)
    {
        var result = BidOutcome.Failed;

        if (BidRow.IsOurs(classes)) result = BidOutcome.Registered;
        else if (rowBidAfter.HasValue && rowBidAfter.Value > ourAmount) result = BidOutcome.Overtaken;

        return result;
    }

    public static uint Ceiling(uint estimate, uint marginCoins, uint maxBid)
    {
        return Math.Min(Pricing.MaxBid(estimate, marginCoins), maxBid);
    }
}
