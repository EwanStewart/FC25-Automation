using Automation.Sbc.Fulfilment;
using Automation.Trading;
using static Automation.Tests.FulfilmentFixtures;

namespace Automation.Tests;

public class GapBuyNowPlanTests
{
    private static AuctionListing Asking(string tradeId, uint buyNowPrice, int expires = 900, int rating = 68)
    {
        return Listing(tradeId, 300, rating) with { BuyNowPrice = buyNowPrice, Expires = expires };
    }

    [Fact]
    public void TheCheapestCardThatCanBeBoughtOutrightIsChosen()
    {
        IReadOnlyList<AuctionListing> listings = [Asking("dear", 800), Asking("cheap", 400), Asking("middle", 600)];

        var choice = GapBuyNowPlan.Cheapest(Specification(), listings, 900);

        Assert.Equal("cheap", choice?.Listing.TradeId);
        Assert.Equal(400u, choice?.Price);
    }

    [Fact]
    public void ACardAskingMoreThanTheCeilingIsNeverBought()
    {
        IReadOnlyList<AuctionListing> listings = [Asking("dear", 901)];

        Assert.Null(GapBuyNowPlan.Cheapest(Specification(), listings, 900));
    }

    [Fact]
    public void ACardAskingExactlyTheCeilingIsStillBought()
    {
        IReadOnlyList<AuctionListing> listings = [Asking("exact", 900)];

        Assert.Equal("exact", GapBuyNowPlan.Cheapest(Specification(), listings, 900)?.Listing.TradeId);
    }

    [Fact]
    public void ACardTheSpecificationDoesNotAcceptIsNeverBought()
    {
        IReadOnlyList<AuctionListing> listings = [Asking("wrong", 400, 900, 40)];

        Assert.Null(GapBuyNowPlan.Cheapest(Specification(), listings, 900));
    }

    [Fact]
    public void AListingWithNoBuyNowPriceIsNeverBought()
    {
        IReadOnlyList<AuctionListing> listings = [Asking("bidonly", 0)];

        Assert.Null(GapBuyNowPlan.Cheapest(Specification(), listings, 900));
    }

    [Fact]
    public void AnAuctionWithNoTimeLeftIsNeverBought()
    {
        IReadOnlyList<AuctionListing> listings = [Asking("gone", 400, 0)];

        Assert.Null(GapBuyNowPlan.Cheapest(Specification(), listings, 900));
    }

    [Fact]
    public void ACardSomebodyElseIsWinningIsNeverBought()
    {
        IReadOnlyList<AuctionListing> listings = [Asking("closed", 400) with { TradeState = "closed" }];

        Assert.Null(GapBuyNowPlan.Cheapest(Specification(), listings, 900));
    }

    [Fact]
    public void TheEarliestEndingCardBreaksATieOnPrice()
    {
        IReadOnlyList<AuctionListing> listings = [Asking("late", 400, 900), Asking("early", 400, 100)];

        Assert.Equal("early", GapBuyNowPlan.Cheapest(Specification(), listings, 900)?.Listing.TradeId);
    }
}
