using Automation.Trading;

namespace Automation.Sbc.Fulfilment;

public sealed record SnipeTarget(TradeState Trade, uint Amount);

public static class GapSnipePlan
{
    public const int WATCH_MAX_SECONDS = 180;
    public const int BID_AIM_SECONDS = 15;
    public const int BATCH_SIZE = 15;

    private const string ACTIVE_STATE = "active";
    private const string HIGHEST_STATE = "highest";

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
            .OrderBy(choice => choice.Price).ThenBy(choice => choice.Listing.Expires)
            .ThenBy(choice => choice.Listing.TradeId, StringComparer.Ordinal)
            .Take(BATCH_SIZE).Select(choice => choice.Listing).ToList();
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
        return targets.FirstOrDefault(trade => trade.State != ACTIVE_STATE && trade.BidState == HIGHEST_STATE);
    }

    public static bool Finished(IReadOnlyList<TradeState> targets)
    {
        return !targets.Any(Running);
    }

    private static bool Ending(AuctionListing listing)
    {
        return listing.Expires > 0 && listing.Expires <= WATCH_MAX_SECONDS;
    }

    private static bool Running(TradeState trade)
    {
        return trade.State == ACTIVE_STATE;
    }

    private static bool Unheld(TradeState trade)
    {
        return trade.BidState != HIGHEST_STATE;
    }

    private static bool Imminent(TradeState trade)
    {
        return trade.SecondsLeft is > 0 and <= BID_AIM_SECONDS;
    }
}
