using Automation.Sbc;
using Automation.Sbc.Fulfilment;

namespace Automation.Tests;

public class FulfilmentResolutionTests
{
    private static readonly MarketSpecification SPEC =
        new("CB", PlayerQuality.Silver, 65, 74, 14, 13, null, null, 900);

    private static GapRecord Gap(GapOutcome outcome, string? tradeId = "t1", int? bid = 500)
    {
        return new GapRecord(0, "CB", SPEC, 1125, outcome, false, "", 1, tradeId, 1, 1, bid, null, "");
    }

    [Fact]
    public void ABidRecordedAgainstTheWrongTradeIsMovedOntoTheOneWeActuallyHold()
    {
        IReadOnlyList<GapRecord> gaps = [Gap(GapOutcome.Bidding)];
        IReadOnlyList<TradeState> states =
            [new TradeState("t1", "closed", "none", 700), new TradeState("t9", "active", "highest", 600, 400, 0, 88)];

        var adopted = StandingBids.Adopted(gaps, states);

        Assert.Equal("t9", adopted[0].TradeId);
        Assert.Equal(88, adopted[0].ItemId);
        Assert.Contains("stands on trade t9 at 600", adopted[0].Detail);
    }

    [Fact]
    public void ATradeAnotherGapAlreadyHoldsIsNeverAdopted()
    {
        IReadOnlyList<GapRecord> gaps =
            [Gap(GapOutcome.Bidding), Gap(GapOutcome.Bidding, "t9") with { SlotIndex = 1 }];
        IReadOnlyList<TradeState> states =
            [new TradeState("t1", "closed", "none", 700), new TradeState("t9", "active", "highest", 600)];

        Assert.Equal("t1", StandingBids.Adopted(gaps, states)[0].TradeId);
    }

    [Fact]
    public void TwoLooseTradesAreLeftAloneBecauseNeitherCanBeToldApart()
    {
        IReadOnlyList<GapRecord> gaps = [Gap(GapOutcome.Bidding)];
        IReadOnlyList<TradeState> states =
            [new TradeState("t8", "active", "highest", 600), new TradeState("t9", "active", "highest", 600)];

        Assert.Equal("t1", StandingBids.Adopted(gaps, states)[0].TradeId);
    }

    [Fact]
    public void AGapWhoseOwnTradeIsStillOursIsNeverMoved()
    {
        IReadOnlyList<GapRecord> gaps = [Gap(GapOutcome.Bidding)];
        IReadOnlyList<TradeState> states =
            [new TradeState("t1", "active", "highest", 500), new TradeState("t9", "active", "highest", 600)];

        Assert.Equal("t1", StandingBids.Adopted(gaps, states)[0].TradeId);
    }

    [Fact]
    public void AStandingBidWeStillLeadOnAnOpenAuctionStaysStanding()
    {
        var resolved = StandingBids.Resolve(Gap(GapOutcome.Bidding), [new TradeState("t1", "active", "highest", 500)]);

        Assert.Equal(GapOutcome.Bidding, resolved.Outcome);
    }

    [Fact]
    public void LeadingAClosedAuctionIsAWin()
    {
        var resolved = StandingBids.Resolve(Gap(GapOutcome.Bidding), [new TradeState("t1", "closed", "highest", 550)]);

        Assert.Equal(GapOutcome.Won, resolved.Outcome);
        Assert.Equal(550, resolved.FinalPrice);
    }

    [Fact]
    public void BeingOutbidOnAnOpenAuctionFreesTheGapToBeRetried()
    {
        var resolved = StandingBids.Resolve(Gap(GapOutcome.Bidding), [new TradeState("t1", "active", "outbid", 700)]);

        Assert.Equal(GapOutcome.Outbid, resolved.Outcome);
        Assert.True(GapProgress.Retryable(resolved.Outcome));
    }

    [Fact]
    public void LosingAClosedAuctionLeavesTheGapExpiredAndRetryable()
    {
        var resolved = StandingBids.Resolve(Gap(GapOutcome.Bidding), [new TradeState("t1", "expired", "outbid", 900)]);

        Assert.Equal(GapOutcome.Expired, resolved.Outcome);
        Assert.True(GapProgress.Retryable(resolved.Outcome));
    }

    [Fact]
    public void ABidWrittenDownButNeverSeenOnTheTradeIsUnresolvedRatherThanRetried()
    {
        var resolved = StandingBids.Resolve(Gap(GapOutcome.Attempting), []);

        Assert.Equal(GapOutcome.Unresolved, resolved.Outcome);
        Assert.False(GapProgress.Retryable(resolved.Outcome));
    }

    [Fact]
    public void AnAttemptTheMarketNeverTookIsFreedToBeRetried()
    {
        var resolved = StandingBids.Resolve(Gap(GapOutcome.Attempting), [new TradeState("t1", "active", "none", 0)]);

        Assert.Equal(GapOutcome.Pending, resolved.Outcome);
        Assert.Null(resolved.BidAmount);
    }

    [Fact]
    public void AnAttemptTheMarketDidTakeBecomesAStandingBid()
    {
        var resolved = StandingBids.Resolve(Gap(GapOutcome.Attempting),
            [new TradeState("t1", "active", "highest", 500)]);

        Assert.Equal(GapOutcome.Bidding, resolved.Outcome);
    }

    [Fact]
    public void AStandingBidThatHasVanishedFromTheMarketIsUnresolved()
    {
        var resolved = StandingBids.Resolve(Gap(GapOutcome.Bidding), []);

        Assert.Equal(GapOutcome.Unresolved, resolved.Outcome);
    }

    [Fact]
    public void AWonGapIsNeverReopened()
    {
        var resolved = StandingBids.Resolve(Gap(GapOutcome.Won), []);

        Assert.Equal(GapOutcome.Won, resolved.Outcome);
    }

    [Fact]
    public void AFinishedGapIsLeftAloneWhateverTheMarketSays()
    {
        var resolved = StandingBids.Resolve(Gap(GapOutcome.Outbid), [new TradeState("t1", "closed", "highest", 550)]);

        Assert.Equal(GapOutcome.Outbid, resolved.Outcome);
    }

    [Fact]
    public void StandingExposureCountsOnlyTheBidsThatCouldStillTakeCoins()
    {
        GapRecord[] gaps =
        [
            Gap(GapOutcome.Bidding) with { SlotIndex = 0, BidAmount = 500 },
            Gap(GapOutcome.Won) with { SlotIndex = 1, BidAmount = 700 },
            Gap(GapOutcome.Outbid) with { SlotIndex = 2, BidAmount = 300 },
            Gap(GapOutcome.Attempting) with { SlotIndex = 3, BidAmount = 400 }
        ];

        var exposure = StandingBids.Exposure(gaps).ToList();

        Assert.Equal([(0, 500L), (1, 700L), (3, 400L)], exposure);
    }
}
