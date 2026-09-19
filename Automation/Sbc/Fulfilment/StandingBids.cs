namespace Automation.Sbc.Fulfilment;

public static class StandingBids
{
    private const string ACTIVE_STATE = BidStates.ACTIVE;

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

    public static IReadOnlyList<GapRecord> Adopted(IReadOnlyList<GapRecord> gaps,
        IReadOnlyList<TradeState> states)
    {
        var loose = Loose(gaps, states);
        var stranded = Stranded(gaps, states);

        return loose.Count == 1 && stranded.Count == 1
            ? gaps.Select(gap => gap == stranded[0] ? Taken(gap, loose[0]) : gap).ToList()
            : gaps;
    }

    private static IReadOnlyList<TradeState> Loose(IReadOnlyList<GapRecord> gaps,
        IReadOnlyList<TradeState> states)
    {
        var spoken = gaps.Select(gap => gap.TradeId).Where(trade => trade is not null).ToHashSet();

        return states.Where(state => BidStates.Held(state.BidState) && !spoken.Contains(state.TradeId)).ToList();
    }

    private static IReadOnlyList<GapRecord> Stranded(IReadOnlyList<GapRecord> gaps,
        IReadOnlyList<TradeState> states)
    {
        return gaps.Where(gap => GapProgress.Holds(gap.Outcome) && gap.Outcome != GapOutcome.Won)
            .Where(gap => !states.Any(state => state.TradeId == gap.TradeId && BidStates.Held(state.BidState)))
            .ToList();
    }

    private static GapRecord Taken(GapRecord gap, TradeState held)
    {
        return gap with
        {
            TradeId = held.TradeId, ItemId = held.ItemId > 0 ? held.ItemId : gap.ItemId, AssetId = null,
            Detail = $"the bid this gap placed stands on trade {held.TradeId} at {held.CurrentBid}"
        };
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
            result = BidStates.Held(trade.BidState)
                ? gap
                : gap with { Outcome = GapOutcome.Outbid, Detail = $"outbid at {trade.CurrentBid}" };
        else if (BidStates.Held(trade.BidState))
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
        return BidStates.Held(trade.BidState) || trade.CurrentBid >= gap.BidAmount.GetValueOrDefault();
    }

    private static GapRecord Unresolved(GapRecord gap, string detail)
    {
        return gap with { Outcome = GapOutcome.Unresolved, Detail = detail };
    }
}
