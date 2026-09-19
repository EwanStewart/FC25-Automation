using Automation.Trading;

namespace Automation.Sbc.Fulfilment;

public sealed record MarketChoice(AuctionListing Listing, uint Price);

public static class MarketCandidateChoice
{
    private const string ACTIVE_STATE = "active";
    private const string HIGHEST_STATE = "highest";

    public static MarketChoice? Best(MarketSpecification specification, IReadOnlyList<AuctionListing> listings,
        uint ceiling)
    {
        return listings.Where(listing => Matches(specification, listing))
            .Select(listing => new MarketChoice(listing, Price(listing)))
            .Where(choice => choice.Price > 0 && choice.Price <= ceiling)
            .OrderBy(choice => choice.Price).ThenBy(choice => choice.Listing.Expires)
            .ThenBy(choice => choice.Listing.TradeId, StringComparer.Ordinal).FirstOrDefault();
    }

    public static IReadOnlyList<AuctionListing> Shown(IReadOnlyList<AuctionListing> listings, int rows)
    {
        return rows > 0 && rows < listings.Count ? listings.Take(rows).ToList() : listings;
    }

    public static uint Price(AuctionListing listing)
    {
        return listing.CurrentBid > 0
            ? listing.CurrentBid + Pricing.BidIncrement(listing.CurrentBid)
            : listing.StartingBid;
    }

    public static bool Matches(MarketSpecification specification, AuctionListing listing)
    {
        return Open(listing) && WithinBand(specification, listing) && PlaysPosition(specification, listing) &&
               PinsHold(specification, listing);
    }

    private static bool Open(AuctionListing listing)
    {
        return listing.TradeState == ACTIVE_STATE && listing.BidState != HIGHEST_STATE;
    }

    private static bool WithinBand(MarketSpecification specification, AuctionListing listing)
    {
        return listing.Rating >= specification.MinimumRating && listing.Rating <= specification.MaximumRating;
    }

    private static bool PlaysPosition(MarketSpecification specification, AuctionListing listing)
    {
        return listing.Position == specification.Position ||
               (listing.PossiblePositions ?? []).Contains(specification.Position);
    }

    private static bool PinsHold(MarketSpecification specification, AuctionListing listing)
    {
        return Holds(specification.NationId, listing.NationId) && Holds(specification.LeagueId, listing.LeagueId) &&
               Holds(specification.ClubId, listing.TeamId) && Holds(specification.RareFlag, listing.RareFlag);
    }

    private static bool Holds(int? pinned, int actual)
    {
        return !pinned.HasValue || pinned.Value == actual;
    }
}
