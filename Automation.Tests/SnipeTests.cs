using Automation.Trading;

namespace Automation.Tests;

public class SnipeTests
{
    private const string PLAIN = "listFUTItem has-auction-data";
    private const uint MARGIN = 1000;
    private const uint MAX_BID = 1500;

    private const int AIM_SECONDS = 15;

    private static bool ShouldBid(string classes, uint? minutes, uint? minimumBid, uint estimate)
    {
        return Snipe.ShouldBid(new TargetFacts(classes, minutes, minimumBid, estimate), MARGIN, MAX_BID, AIM_SECONDS);
    }

    private static bool ShouldBidWithModel(string classes, int secondsLeft, string bidState)
    {
        return Snipe.ShouldBid(new TargetFacts(classes, 0, 300, 2000, secondsLeft, bidState), MARGIN, MAX_BID,
            AIM_SECONDS);
    }

    [Fact]
    public void ExactSecondsTakePrecedenceOverTheMinuteText()
    {
        Assert.True(ShouldBidWithModel(PLAIN, 15, "none"));
        Assert.True(ShouldBidWithModel(PLAIN, 3, "outbid"));
        Assert.False(ShouldBidWithModel(PLAIN, 16, "none"));
        Assert.False(ShouldBidWithModel(PLAIN, 5, "highest"));
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
    public void ModelTradeStateEndsARowTheDomStillShowsAsLive()
    {
        Assert.True(Snipe.IsLive(PLAIN, "active"));
        Assert.True(Snipe.IsLive(PLAIN, null));
        Assert.False(Snipe.IsLive(PLAIN, "expired"));
        Assert.False(Snipe.IsLive($"{PLAIN} expired", "active"));
    }

    [Theory]
    [InlineData(10, 1000)]
    [InlineData(29, 1000)]
    [InlineData(30, 5000)]
    [InlineData(59, 5000)]
    [InlineData(60, 120000)]
    [InlineData(599, 120000)]
    [InlineData(600, 600000)]
    public void RefreshIntervalFollowsTheAppsTiers(int secondsLeft, int expectedMs)
    {
        Assert.Equal(expectedMs, Snipe.RefreshIntervalMs(secondsLeft));
    }

    [Fact]
    public void RowIsFrozenWhenItsAgeExceedsTwiceTheRefreshIntervalOfTheTierItWasLastUpdatedIn()
    {
        Assert.False(Snipe.IsFrozen(1500, 20));
        Assert.True(Snipe.IsFrozen(2001, 20));
        Assert.False(Snipe.IsFrozen(9000, 45));
        Assert.True(Snipe.IsFrozen(10001, 45));
        Assert.False(Snipe.IsFrozen(100000, 300));
        Assert.False(Snipe.IsFrozen(240001, 300));
        Assert.False(Snipe.IsFrozen(30000, 61));
        Assert.False(Snipe.IsFrozen(4000, 28));
        Assert.False(Snipe.IsFrozen(100000, 58));
        Assert.False(Snipe.IsFrozen(null, 20));
        Assert.False(Snipe.IsFrozen(5000, null));
    }

    [Fact]
    public void FailedStatusRefreshFreezesRowsExceptA401TheClientRetries()
    {
        Assert.False(Snipe.StatusFreezesRows(200));
        Assert.False(Snipe.StatusFreezesRows(401));
        Assert.True(Snipe.StatusFreezesRows(512));
        Assert.True(Snipe.StatusFreezesRows(429));
    }

    [Fact]
    public void SacrificialRowIsLiveNotOursAndAlreadyOverItsCeiling()
    {
        Assert.True(Snipe.IsSacrificial(new TargetFacts(PLAIN, 3, 1000, 2000), MARGIN, MAX_BID, AIM_SECONDS));
        Assert.False(Snipe.IsSacrificial(new TargetFacts(PLAIN, 3, 900, 2000), MARGIN, MAX_BID, AIM_SECONDS));
        Assert.False(Snipe.IsSacrificial(new TargetFacts($"{PLAIN} highest-bid", 3, 1000, 2000), MARGIN, MAX_BID, AIM_SECONDS));
        Assert.False(Snipe.IsSacrificial(new TargetFacts($"{PLAIN} expired", 3, 1000, 2000), MARGIN, MAX_BID, AIM_SECONDS));
        Assert.False(Snipe.IsSacrificial(new TargetFacts(PLAIN, 3, null, 2000), MARGIN, MAX_BID, AIM_SECONDS));
    }

    [Fact]
    public void WatchIsConfirmedByAnEnabledUnwatchButtonAndNoRefusalFromTheServer()
    {
        Assert.True(Snipe.WatchConfirmed(true, 200, true));
        Assert.True(Snipe.WatchConfirmed(true, null, false));
        Assert.False(Snipe.WatchConfirmed(true, null, true));
        Assert.False(Snipe.WatchConfirmed(true, 461, true));
        Assert.False(Snipe.WatchConfirmed(false, 200, true));
    }

    [Fact]
    public void BiddingOutranksTheRefreshWhenAnyRowIsDue()
    {
        Assert.False(Snipe.ShouldRefresh(true, true, true));
        Assert.True(Snipe.ShouldRefresh(false, true, false));
        Assert.True(Snipe.ShouldRefresh(false, false, true));
        Assert.False(Snipe.ShouldRefresh(false, false, false));
    }

    [Fact]
    public void ARowInsideTheBidWindowIsNeverSacrificed()
    {
        var facts = new TargetFacts(PLAIN, 0, 2000, 2000, 8, "none");

        Assert.False(Snipe.IsSacrificial(facts, MARGIN, MAX_BID, AIM_SECONDS));
        Assert.True(Snipe.IsSacrificial(facts with { SecondsLeft = 60 }, MARGIN, MAX_BID, AIM_SECONDS));
        Assert.False(Snipe.IsSacrificial(facts with { SecondsLeft = 60, MinimumBid = 300 }, MARGIN, MAX_BID, AIM_SECONDS));
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
    public void ModelBidStateSettlesTheOutcomeWhenPresent()
    {
        Assert.Equal(BidOutcome.Registered, Snipe.Outcome(PLAIN, 900, 900, "highest"));
        Assert.Equal(BidOutcome.Overtaken, Snipe.Outcome(PLAIN, 900, 1000, "outbid"));
        Assert.Equal(BidOutcome.Failed, Snipe.Outcome(PLAIN, 900, 900, "none"));
    }

    [Fact]
    public void StaleOutbidStateWithOurAmountShowingIsNotSettledYet()
    {
        Assert.Equal(BidOutcome.Failed, Snipe.Outcome($"{PLAIN} outbid", 900, 900, "outbid"));
    }

    [Fact]
    public void RowUnchangedAfterTheClickMeansTheBidFailed()
    {
        Assert.Equal(BidOutcome.Failed, Snipe.Outcome($"{PLAIN} outbid", 900, 900));
        Assert.Equal(BidOutcome.Failed, Snipe.Outcome(PLAIN, 300, null));
    }
}
