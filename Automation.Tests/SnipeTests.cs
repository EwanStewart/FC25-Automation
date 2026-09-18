using Automation.Trading;

namespace Automation.Tests;

public class SnipeTests
{
    private const string PLAIN = "listFUTItem has-auction-data";
    private const uint MARGIN = 1000;
    private const uint MAX_BID = 1500;

    private static bool ShouldBid(string classes, uint? minutes, uint? minimumBid, uint estimate)
    {
        return Snipe.ShouldBid(new TargetFacts(classes, minutes, minimumBid, estimate), MARGIN, MAX_BID);
    }

    [Fact]
    public void WatchedRowUnderAMinuteWithAffordableMinimumIsBid()
    {
        Assert.True(ShouldBid(PLAIN, 0, 300, 2000));
    }

    [Fact]
    public void OutbidRowUnderAMinuteIsBidAgainWhileTheMinimumClearsTheMargin()
    {
        Assert.True(ShouldBid($"{PLAIN} outbid", 0, 850, 2000));
        Assert.False(ShouldBid($"{PLAIN} outbid", 0, 950, 2000));
    }

    [Fact]
    public void RowsWeLeadOrThatHaveEndedAreNotBid()
    {
        Assert.False(ShouldBid($"{PLAIN} highest-bid", 0, 300, 2000));
        Assert.False(ShouldBid($"{PLAIN} expired", 0, 300, 2000));
        Assert.False(ShouldBid($"{PLAIN} won", 0, 300, 2000));
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(5u)]
    public void RowsWithAMinuteOrMoreLeftWait(uint minutes)
    {
        Assert.False(ShouldBid(PLAIN, minutes, 300, 2000));
        Assert.False(ShouldBid(PLAIN, null, 300, 2000));
    }

    [Fact]
    public void MinimumAboveTheHardCapIsNotBidEvenWhenTheMarginHolds()
    {
        Assert.False(ShouldBid(PLAIN, 0, 1600, 10000));
        Assert.False(ShouldBid(PLAIN, 0, null, 10000));
    }

    [Fact]
    public void WatchingStopsAtTheBatchSizeAndTheCeiling()
    {
        Assert.True(Snipe.ShouldWatch(2000, 300, MARGIN, MAX_BID, 14, 15));
        Assert.False(Snipe.ShouldWatch(2000, 300, MARGIN, MAX_BID, 15, 15));
        Assert.False(Snipe.ShouldWatch(1300, 300, MARGIN, MAX_BID, 0, 15));
        Assert.False(Snipe.ShouldWatch(10000, 1600, MARGIN, MAX_BID, 0, 15));
    }

    [Fact]
    public void BatchFinishesWhenNoWatchedRowIsStillLive()
    {
        Assert.False(Snipe.BatchFinished(new[] { $"{PLAIN} expired", $"{PLAIN} outbid" }));
        Assert.True(Snipe.BatchFinished(new[] { $"{PLAIN} expired", $"{PLAIN} won" }));
        Assert.True(Snipe.BatchFinished(Array.Empty<string>()));
    }
}

public class SnipeBidOutcomeTests
{
    private const string PLAIN = "listFUTItem has-auction-data";

    [Fact]
    public void RowShowingOurHighestBidMeansTheBidRegistered()
    {
        Assert.Equal(BidOutcome.Registered, Snipe.Outcome($"{PLAIN} highest-bid", 900, 950));
    }

    [Fact]
    public void RowOutbidAboveOurAmountMeansWeWereOvertakenNotThatTheBidFailed()
    {
        Assert.Equal(BidOutcome.Overtaken, Snipe.Outcome($"{PLAIN} outbid", 900, 1000));
        Assert.Equal(BidOutcome.Overtaken, Snipe.Outcome(PLAIN, 900, 1000));
    }

    [Fact]
    public void RowUnchangedAfterTheClickMeansTheBidFailed()
    {
        Assert.Equal(BidOutcome.Failed, Snipe.Outcome($"{PLAIN} outbid", 900, 900));
        Assert.Equal(BidOutcome.Failed, Snipe.Outcome(PLAIN, 300, null));
    }
}
