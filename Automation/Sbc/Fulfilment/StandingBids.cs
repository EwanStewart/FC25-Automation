namespace Automation.Sbc.Fulfilment;

public static class StandingBids
{
    private const string HIGHEST_STATE = "highest";
    private const string ACTIVE_STATE = "active";

    public static GapRecord Resolve(GapRecord gap, IReadOnlyList<TradeState> states)
    {
        var trade = gap.TradeId is null
            ? null
            : states.FirstOrDefault(state => state.TradeId == gap.TradeId);
        var result = gap;

        if (gap.Outcome == GapOutcome.Bidding) result = ResolveStanding(gap, trade);
        else if (gap.Outcome == GapOutcome.Attempting) result = ResolveAttempt(gap, trade);

        return result;
    }

    public static IEnumerable<(int Slot, long Amount)> Exposure(IEnumerable<GapRecord> gaps)
    {
        return gaps.Where(gap => GapProgress.Holds(gap.Outcome) && gap.BidAmount.HasValue)
            .Select(gap => (gap.SlotIndex, (long)gap.BidAmount!.Value));
    }

    private static GapRecord ResolveStanding(GapRecord gap, TradeState? trade)
    {
        GapRecord result;

        if (trade is null) result = Unresolved(gap, "the trade is no longer on the transfer targets");
        else if (trade.State == ACTIVE_STATE)
            result = trade.BidState == HIGHEST_STATE
                ? gap
                : gap with { Outcome = GapOutcome.Outbid, Detail = $"outbid at {trade.CurrentBid}" };
        else if (trade.BidState == HIGHEST_STATE)
            result = gap with
            {
                Outcome = GapOutcome.Won, FinalPrice = (int)trade.CurrentBid, Detail = $"won at {trade.CurrentBid}"
            };
        else result = gap with { Outcome = GapOutcome.Expired, Detail = $"lost at {trade.CurrentBid}" };

        return result;
    }

    private static GapRecord ResolveAttempt(GapRecord gap, TradeState? trade)
    {
        GapRecord result;

        if (trade is null) result = Unresolved(gap, "a bid was written down but the trade cannot be read back");
        else if (TookOurBid(gap, trade)) result = ResolveStanding(gap with { Outcome = GapOutcome.Bidding }, trade);
        else result = gap with { Outcome = GapOutcome.Pending, BidAmount = null, Detail = "the bid never landed" };

        return result;
    }

    private static bool TookOurBid(GapRecord gap, TradeState trade)
    {
        return trade.BidState == HIGHEST_STATE || trade.CurrentBid >= gap.BidAmount.GetValueOrDefault();
    }

    private static GapRecord Unresolved(GapRecord gap, string detail)
    {
        return gap with { Outcome = GapOutcome.Unresolved, Detail = detail };
    }
}
