using Automation.Trading;

namespace Automation.Sbc.Fulfilment;

public sealed record SnipeTarget(TradeState Trade, uint Amount);

public static class GapSnipePlan
{
    public const int MAX_WAIT_SECONDS = 1800;
    public const int WAIT_MARGIN_SECONDS = 45;
    public const int BID_AIM_SECONDS = 15;
    public const int BATCH_SIZE = 15;

    private const string ACTIVE_STATE = BidStates.ACTIVE;

    public static IReadOnlyList<TradeState> Watched(IReadOnlyList<TradeState> targets,
        IReadOnlyList<string> tradeIds)
    {
        return targets.Where(trade => tradeIds.Contains(trade.TradeId)).ToList();
    }

    public static IReadOnlyList<AuctionListing> Shortlist(MarketSpecification specification,
        IReadOnlyList<AuctionListing> listings, uint ceiling)
    {
        return listings.Where(listing => MarketCandidateChoice.Matches(specification, listing))
            .Where(Ending)
            .Select(listing => new MarketChoice(listing, MarketCandidateChoice.Price(listing)))
            .Where(choice => choice.Price > 0 && choice.Price <= ceiling)
            .OrderBy(choice => choice.Listing.Expires).ThenBy(choice => choice.Price)
            .ThenBy(choice => choice.Listing.TradeId, StringComparer.Ordinal)
            .Take(BATCH_SIZE).Select(choice => choice.Listing).ToList();
    }

    public static int Wait(IReadOnlyList<AuctionListing> shortlist)
    {
        var soonest = shortlist.Count == 0 ? 0 : shortlist.Min(listing => listing.Expires);

        return Math.Min(soonest + WAIT_MARGIN_SECONDS, MAX_WAIT_SECONDS);
    }

    public static SnipeTarget? Due(IReadOnlyList<TradeState> targets, uint ceiling)
    {
        return Leading(targets) ? null : Next(targets, ceiling);
    }

    private static SnipeTarget? Next(IReadOnlyList<TradeState> targets, uint ceiling)
    {
        return targets.Where(Running).Where(Unheld).Where(Imminent)
            .Where(trade => trade.MinimumBid > 0 && trade.MinimumBid <= ceiling)
            .OrderBy(trade => trade.SecondsLeft)
            .Select(trade => new SnipeTarget(trade, trade.MinimumBid)).FirstOrDefault();
    }

    private static bool Leading(IReadOnlyList<TradeState> targets)
    {
        return targets.Any(trade => Running(trade) && !Unheld(trade));
    }

    public static TradeState? Won(IReadOnlyList<TradeState> targets)
    {
        return targets.FirstOrDefault(trade => trade.State != ACTIVE_STATE && BidStates.Held(trade.BidState));
    }

    public static bool Finished(IReadOnlyList<TradeState> targets)
    {
        return !targets.Any(Running);
    }

    private static bool Ending(AuctionListing listing)
    {
        return listing.Expires > 0 && listing.Expires <= MAX_WAIT_SECONDS;
    }

    private static bool Running(TradeState trade)
    {
        return trade.State == ACTIVE_STATE;
    }

    private static bool Unheld(TradeState trade)
    {
        return !BidStates.Held(trade.BidState);
    }

    private static bool Imminent(TradeState trade)
    {
        return trade.SecondsLeft is > 0 and <= BID_AIM_SECONDS;
    }
}
