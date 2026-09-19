using Automation.Sbc;
using Automation.Sbc.Fulfilment;
using Automation.Trading;
using static Automation.Tests.FulfilmentFixtures;

namespace Automation.Tests;

public class GapSnipePlanTests
{
    private static AuctionListing Ending(string tradeId, uint startingBid, int expires, int rating = 68)
    {
        return Listing(tradeId, startingBid, rating) with { Expires = expires };
    }

    private static TradeState Target(string tradeId, int? secondsLeft, uint minimumBid, string bidState = "none",
        string state = "active")
    {
        return new TradeState(tradeId, state, bidState, 0, secondsLeft, minimumBid, 55);
    }

    [Fact]
    public void OnlyTheCardsCloseToEndingAreWorthWatching()
    {
        IReadOnlyList<AuctionListing> listings =
            [Ending("late", 300, 900), Ending("soon", 300, 170), Ending("now", 300, 20)];

        var shortlist = GapSnipePlan.Shortlist(Specification(), listings, 900);

        Assert.Equal(["now", "soon"], shortlist.Select(listing => listing.TradeId).Order());
    }

    [Fact]
    public void ACardPricedOverTheCeilingIsNeverWatched()
    {
        IReadOnlyList<AuctionListing> listings = [Ending("cheap", 300, 100), Ending("dear", 2000, 100)];

        var shortlist = GapSnipePlan.Shortlist(Specification(), listings, 900);

        Assert.Equal(["cheap"], shortlist.Select(listing => listing.TradeId));
    }

    [Fact]
    public void ACardTheSpecificationDoesNotAcceptIsNeverWatched()
    {
        IReadOnlyList<AuctionListing> listings = [Ending("fits", 300, 100), Ending("wrong", 300, 100, 40)];

        var shortlist = GapSnipePlan.Shortlist(Specification(), listings, 900);

        Assert.Equal(["fits"], shortlist.Select(listing => listing.TradeId));
    }

    [Fact]
    public void TheShortlistIsCappedAndTakesTheCheapestFirst()
    {
        var listings = Enumerable.Range(0, GapSnipePlan.BATCH_SIZE + 5)
            .Select(index => Ending($"t{index}", (uint)(800 - index), 100)).ToList();

        var shortlist = GapSnipePlan.Shortlist(Specification(), listings, 900);

        Assert.Equal(GapSnipePlan.BATCH_SIZE, shortlist.Count);
        Assert.Equal($"t{GapSnipePlan.BATCH_SIZE + 4}", shortlist[0].TradeId);
    }

    [Fact]
    public void OnlyACardInsideTheLastSecondsIsBidOnAndOnlyOne()
    {
        IReadOnlyList<TradeState> targets =
            [Target("early", 90, 300), Target("due", 12, 300), Target("later", 40, 300)];

        var due = GapSnipePlan.Due(targets, 900);

        Assert.Equal("due", due?.Trade.TradeId);
        Assert.Equal(300u, due?.Amount);
    }

    [Fact]
    public void ACardWeAreAlreadyWinningIsNotBidOnAgain()
    {
        IReadOnlyList<TradeState> targets = [Target("ours", 10, 300, "highest")];

        Assert.Null(GapSnipePlan.Due(targets, 900));
    }

    [Fact]
    public void ACardWhoseNextBidBreaksTheCeilingIsLeftAlone()
    {
        IReadOnlyList<TradeState> targets = [Target("dear", 10, 1200)];

        Assert.Null(GapSnipePlan.Due(targets, 900));
    }

    [Fact]
    public void ACardThatHasAlreadyFinishedIsNotBidOn()
    {
        IReadOnlyList<TradeState> targets = [Target("gone", 0, 300, "outbid", "closed")];

        Assert.Null(GapSnipePlan.Due(targets, 900));
    }

    [Fact]
    public void AClosedCardWeHoldTheTopBidOnIsTheOneWeWon()
    {
        IReadOnlyList<TradeState> targets =
            [Target("lost", 0, 0, "outbid", "closed"), Target("won", 0, 0, "highest", "closed")];

        Assert.Equal("won", GapSnipePlan.Won(targets)?.TradeId);
    }

    [Fact]
    public void NothingIsWonWhileTheAuctionIsStillRunning()
    {
        Assert.Null(GapSnipePlan.Won([Target("ours", 10, 300, "highest")]));
    }

    [Fact]
    public void TheBatchIsFinishedOnceNoWatchedCardIsStillRunning()
    {
        Assert.True(GapSnipePlan.Finished([Target("gone", 0, 0, "outbid", "closed")]));
        Assert.False(GapSnipePlan.Finished([Target("live", 30, 300)]));
        Assert.True(GapSnipePlan.Finished([]));
    }
}
