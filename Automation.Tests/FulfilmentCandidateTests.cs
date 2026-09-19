using Automation.Sbc;
using Automation.Sbc.Fulfilment;
using Automation.Trading;

namespace Automation.Tests;

public class FulfilmentCandidateTests
{
    private const int NATION = 14;
    private const int LEAGUE = 13;
    private const int CLUB = 1;

    private static MarketSpecification Specification(int? nation = NATION, int? league = LEAGUE, int? club = null,
        int? rarity = null, int minimum = 65, int maximum = 74)
    {
        return new MarketSpecification("CB", PlayerQuality.Silver, minimum, maximum, nation, league, club, rarity,
            900);
    }

    private static AuctionListing Listing(string tradeId, uint startingBid, uint currentBid = 0, int rating = 68,
        string position = "CB", int nation = NATION, int league = LEAGUE, int club = CLUB, int rare = 1,
        int expires = 600, string tradeState = "active", string bidState = "none")
    {
        return new AuctionListing(tradeId, tradeState, bidState, expires, currentBid, startingBid, 0, rating,
            position, 0, 0, 0, 0, club, league, nation, rare, []);
    }

    [Fact]
    public void TheCheapestListingInsideTheCeilingWins()
    {
        AuctionListing[] listings =
        [
            Listing("a", 400),
            Listing("b", 250),
            Listing("c", 300)
        ];

        var chosen = MarketCandidateChoice.Best(Specification(), listings, 1000);

        Assert.Equal("b", chosen?.Listing.TradeId);
        Assert.Equal(250u, chosen?.Price);
    }

    [Fact]
    public void ALiveBidIsMetAtTheNextIncrementNotTheStartingPrice()
    {
        var chosen = MarketCandidateChoice.Best(Specification(), [Listing("a", 200, 500)], 1000);

        Assert.Equal(550u, chosen?.Price);
    }

    [Fact]
    public void NothingAboveTheCeilingIsChosen()
    {
        AuctionListing[] listings = [Listing("a", 1200), Listing("b", 1001)];

        Assert.Null(MarketCandidateChoice.Best(Specification(), listings, 1000));
    }

    [Fact]
    public void ACardMissingAPinnedAttributeIsRejected()
    {
        Assert.Null(MarketCandidateChoice.Best(Specification(), [Listing("a", 300, nation: 21)], 1000));
        Assert.Null(MarketCandidateChoice.Best(Specification(), [Listing("a", 300, league: 16)], 1000));
        Assert.Null(MarketCandidateChoice.Best(Specification(club: CLUB), [Listing("a", 300, club: 9)], 1000));
        Assert.Null(MarketCandidateChoice.Best(Specification(rarity: 3), [Listing("a", 300, rare: 1)], 1000));
    }

    [Fact]
    public void ACardOutsideTheRatingBandIsRejected()
    {
        Assert.Null(MarketCandidateChoice.Best(Specification(), [Listing("a", 300, rating: 64)], 1000));
        Assert.Null(MarketCandidateChoice.Best(Specification(), [Listing("a", 300, rating: 75)], 1000));
    }

    [Fact]
    public void ACardThatCannotPlayTheSlotPositionIsRejected()
    {
        Assert.Null(MarketCandidateChoice.Best(Specification(), [Listing("a", 300, position: "ST")], 1000));
    }

    [Fact]
    public void ACardThatListsTheSlotPositionAmongItsAlternativesIsAccepted()
    {
        AuctionListing listing = Listing("a", 300, position: "LB") with { PossiblePositions = ["LB", "CB"] };

        Assert.Equal("a", MarketCandidateChoice.Best(Specification(), [listing], 1000)?.Listing.TradeId);
    }

    [Fact]
    public void EndedAuctionsAndOurOwnLeadingBidsAreIgnored()
    {
        Assert.Null(MarketCandidateChoice.Best(Specification(), [Listing("a", 300, tradeState: "expired")], 1000));
        Assert.Null(MarketCandidateChoice.Best(Specification(), [Listing("a", 300, tradeState: "closed")], 1000));
        Assert.Null(MarketCandidateChoice.Best(Specification(), [Listing("a", 300, bidState: "highest")], 1000));
    }

    [Fact]
    public void EqualPricesAreSettledByWhicheverEndsSoonest()
    {
        AuctionListing[] listings = [Listing("a", 300, expires: 900), Listing("b", 300, expires: 120)];

        Assert.Equal("b", MarketCandidateChoice.Best(Specification(), listings, 1000)?.Listing.TradeId);
    }

    [Fact]
    public void AnUnpinnedSpecificationAcceptsAnyNationOrLeague()
    {
        var chosen = MarketCandidateChoice.Best(Specification(null, null), [Listing("a", 300, nation: 99,
            league: 99)], 1000);

        Assert.Equal("a", chosen?.Listing.TradeId);
    }
}
