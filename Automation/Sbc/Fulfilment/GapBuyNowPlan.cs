using Automation.Trading;

namespace Automation.Sbc.Fulfilment;

public static class GapBuyNowPlan
{
    public static MarketChoice? Cheapest(MarketSpecification specification, IReadOnlyList<AuctionListing> listings,
        uint ceiling)
    {
        return listings.Where(listing => MarketCandidateChoice.Matches(specification, listing))
            .Where(listing => Buyable(listing, ceiling))
            .Select(listing => new MarketChoice(listing, listing.BuyNowPrice))
            .OrderBy(choice => choice.Price).ThenBy(choice => choice.Listing.Expires)
            .ThenBy(choice => choice.Listing.TradeId, StringComparer.Ordinal).FirstOrDefault();
    }

    private static bool Buyable(AuctionListing listing, uint ceiling)
    {
        return listing.Expires > 0 && listing.BuyNowPrice > 0 && listing.BuyNowPrice <= ceiling;
    }
}
