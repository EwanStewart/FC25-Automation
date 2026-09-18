using Automation.Flow;

namespace Automation.Trading;

public readonly record struct RowFacts(
    string Classes,
    uint? MinutesLeft,
    bool AlreadyBid,
    uint? RowBid,
    uint? CachedResale,
    bool HasSales);

public static class RowTriage
{
    public static bool IsCandidate(RowFacts facts, uint minMinutes, uint maxMinutes, uint marginCoins)
    {
        var inWindow = facts.MinutesLeft.HasValue &&
                       Pricing.IsWithinBidWindow(facts.MinutesLeft.Value, minMinutes, maxMinutes);
        var result = inWindow && !BidRow.IsOurs(facts.Classes) && !facts.AlreadyBid;

        if (result && facts.CachedResale.HasValue && !facts.HasSales)
            result = facts.CachedResale.Value >= Pricing.RequiredResale(facts.RowBid ?? 0, marginCoins);

        return result;
    }
}
