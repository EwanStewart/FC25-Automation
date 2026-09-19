using Automation.Sbc.Fulfilment;
using Automation.Trading;
using static Automation.Tests.FulfilmentFixtures;

namespace Automation.Tests;

public class FulfilmentBuyingTests
{
    private static (BuyingResult result, RecordingStore store, List<string> log) Buy(FulfilmentRun run,
        IReadOnlyList<GapRecord> gaps, Dictionary<int, IReadOnlyList<AuctionListing>> results,
        Dictionary<string, BidOutcome>? outcomes = null, IReadOnlyList<TradeState>? standing = null,
        IReadOnlyList<IReadOnlyList<TradeState>>? polls = null)
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedMarket market = new(log, results, outcomes, standing);

        foreach (var poll in polls ?? []) market.Polls.Enqueue(poll);

        GapBuyer buyer = new(market, store);

        return (buyer.Buy(run, gaps), store, log);
    }

    private static TradeState Due(string tradeId, uint minimumBid, int secondsLeft = 10)
    {
        return new TradeState(tradeId, "active", "none", 0, secondsLeft, minimumBid, 77);
    }

    private static TradeState Ended(string tradeId, uint finalBid, string bidState)
    {
        return new TradeState(tradeId, "closed", bidState, finalBid, 0, 0, 77);
    }

    [Fact]
    public void ADryRunRecordsWhatItWouldBuyAndPlacesNoBid()
    {
        var (result, store, log) = Buy(Run(FulfilmentMode.Dry, 3000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 800)] });

        Assert.DoesNotContain(log, entry => entry.StartsWith("bid"));
        Assert.Equal(GapOutcome.Simulated, store.Gaps(1)[0].Outcome);
        Assert.True(store.Gaps(1)[0].Simulated);
        Assert.Equal(800, store.Gaps(1)[0].BidAmount);
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void ADryRunLeavesTheApprovalQueuedSoALiveRunStillPicksItUp()
    {
        var (result, store, _) = Buy(Run(FulfilmentMode.Dry, 3000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 800)] });

        Assert.Equal(FulfilmentState.Buying, result.State);
        Assert.Equal(FulfilmentState.Pending, store.State);
        Assert.Contains("dry run", store.Detail);
    }

    [Fact]
    public void ADryRunThatHitsTheCeilingStillLeavesTheApprovalQueued()
    {
        var (result, store, _) = Buy(Run(FulfilmentMode.Dry, 1000), [Gap(0), Gap(1)],
            new Dictionary<int, IReadOnlyList<AuctionListing>>
            {
                [0] = [Listing("t1", 900)],
                [1] = [Listing("t2", 900)]
            });

        Assert.Equal(FulfilmentState.Aborted, result.State);
        Assert.Equal(FulfilmentState.Pending, store.State);
    }

    [Fact]
    public void ALiveRunRecordsTheStateItActuallyReached()
    {
        var (_, store, _) = Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 800)] });

        Assert.Equal(FulfilmentState.Buying, store.State);
    }

    [Fact]
    public void TheRunCeilingStopsTheWholeFulfilmentRatherThanBiddingOn()
    {
        var (result, store, log) = Buy(Run(FulfilmentMode.Live, 1000), [Gap(0), Gap(1)],
            new Dictionary<int, IReadOnlyList<AuctionListing>>
            {
                [0] = [Listing("t1", 900)],
                [1] = [Listing("t2", 900)]
            }, polls: [[Due("t1", 900)]]);

        Assert.Equal(FulfilmentState.Aborted, result.State);
        Assert.Contains("ceiling", result.Detail);
        Assert.Single(log.Where(entry => entry.StartsWith("bid")));
        Assert.Equal(GapOutcome.Bidding, store.Gaps(1)[0].Outcome);
    }

    [Fact]
    public void ADryRunHitsTheSameCeilingTheLiveRunWould()
    {
        var (result, _, log) = Buy(Run(FulfilmentMode.Dry, 1000), [Gap(0), Gap(1)],
            new Dictionary<int, IReadOnlyList<AuctionListing>>
            {
                [0] = [Listing("t1", 900)],
                [1] = [Listing("t2", 900)]
            });

        Assert.Equal(FulfilmentState.Aborted, result.State);
        Assert.Single(log.Where(entry => entry.StartsWith("search")));
    }

    [Fact]
    public void AMarketOnlyOfferingCardsAboveThePerCardCeilingBuysNothing()
    {
        var (result, store, log) = Buy(Run(FulfilmentMode.Live, 100000), [Gap(0, 900)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 4000)] });

        Assert.DoesNotContain(log, entry => entry.StartsWith("bid"));
        Assert.Equal(GapOutcome.TooExpensive, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void ThePerCardCeilingIsWhatTheSearchAsksFor()
    {
        List<string> log = [];
        ScriptedMarket market = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>>());
        GapBuyer buyer = new(market, new RecordingStore(log));

        buyer.Buy(Run(FulfilmentMode.Live, 100000), [Gap(0, 900)]);

        Assert.Equal(CardCeiling.For(900), (int)market.Ceilings[0]);
    }

    [Fact]
    public void AnEmptyMarketIsRecordedAsNotFound()
    {
        var (result, store, _) = Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [] });

        Assert.Equal(GapOutcome.NotFound, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void ABidIsWrittenDownBeforeItIsPlaced()
    {
        var (_, _, log) = Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 800)] },
            polls: [[Due("t1", 800)]]);

        var written = log.FindIndex(entry => entry == "gap 0 Attempting 800");
        var placed = log.FindIndex(entry => entry.StartsWith("bid"));

        Assert.True(written >= 0);
        Assert.True(written < placed);
    }

    [Fact]
    public void TheCardsOnThePageAreWatchedRatherThanBidOnStraightAway()
    {
        var (_, _, log) = Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>>
            {
                [0] = [Listing("t1", 300), Listing("t2", 400)]
            });

        Assert.Contains("watch t1,t2", log);
        Assert.DoesNotContain(log, entry => entry.StartsWith("bid"));
    }

    [Fact]
    public void NoBidGoesOutUntilTheCardIsInsideItsLastSeconds()
    {
        var (_, _, log) = Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 300)] },
            polls: [[Due("t1", 300, 90)], [Due("t1", 300, 40)], [Due("t1", 300, 9)]]);

        Assert.Single(log.Where(entry => entry.StartsWith("bid")));
        Assert.Contains("bid t1 300", log);
    }

    [Fact]
    public void AWonCardGoesToTheClubAndNothingElseIsBidOn()
    {
        var (_, store, log) = Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>>
            {
                [0] = [Listing("t1", 300), Listing("t2", 400)]
            },
            polls: [[Due("t1", 300), Due("t2", 400, 90)], [Ended("t1", 300, "highest"), Due("t2", 400, 5)]]);

        Assert.Contains("bid t1 300", log);
        Assert.Contains("claim t1", log);
        Assert.DoesNotContain("bid t2 400", log);
        Assert.Equal(GapOutcome.Won, store.Gaps(1)[0].Outcome);
        Assert.Contains("sent to the club", store.Gaps(1)[0].Detail);
    }

    [Fact]
    public void TheCardWeWonIsTheOneWrittenDownAgainstTheGap()
    {
        var (_, store, _) = Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 300)] },
            polls: [[Due("t1", 300)], [Ended("t1", 350, "highest")]]);

        Assert.Equal("t1", store.Gaps(1)[0].TradeId);
        Assert.Equal(77, store.Gaps(1)[0].ItemId);
        Assert.Equal(350, store.Gaps(1)[0].FinalPrice);
    }

    [Fact]
    public void ACardTheMarketWillNotWatchIsRecordedRatherThanBidOn()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedMarket market = new(log,
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 300)] })
        {
            WatchRefused = true
        };

        new GapBuyer(market, store).Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)]);

        Assert.DoesNotContain(log, entry => entry.StartsWith("bid"));
        Assert.Equal(GapOutcome.NotFound, store.Gaps(1)[0].Outcome);
    }

    [Fact]
    public void ACardThatEndsTooFarAwayIsNeverWatched()
    {
        var (_, store, log) = Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>>
            {
                [0] = [Listing("t1", 300) with { Expires = 900 }]
            });

        Assert.DoesNotContain(log, entry => entry.StartsWith("watch"));
        Assert.Contains("ends within", store.Gaps(1)[0].Detail);
    }

    [Fact]
    public void AGapAlreadyWonIsNeverSearchedForAgain()
    {
        var won = Gap(0) with { Outcome = GapOutcome.Won, TradeId = "t1", BidAmount = 800, FinalPrice = 800 };
        var (result, store, log) = Buy(Run(FulfilmentMode.Live, 100000), [won],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t9", 400)] });

        Assert.Empty(log.Where(entry => entry.StartsWith("search")));
        Assert.Equal(GapOutcome.Won, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void AGapWeWereOutbidOnIsBidAgain()
    {
        var outbid = Gap(0) with { Outcome = GapOutcome.Outbid, TradeId = "t1", BidAmount = 800 };
        var (_, store, log) = Buy(Run(FulfilmentMode.Live, 100000), [outbid],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t2", 900)] },
            polls: [[Due("t2", 900)]]);

        Assert.Contains("bid t2 900", log);
        Assert.Equal(GapOutcome.Bidding, store.Gaps(1)[0].Outcome);
    }

    [Fact]
    public void AnExpiredAuctionFreesTheGapAndTheNextRunBuysAgain()
    {
        var bidding = Gap(0) with { Outcome = GapOutcome.Bidding, TradeId = "t1", BidAmount = 800 };
        var (_, store, log) = Buy(Run(FulfilmentMode.Live, 100000), [bidding],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t2", 700)] },
            polls: [[new TradeState("t1", "expired", "outbid", 1200)], [Due("t2", 700)]]);

        Assert.Contains("bid t2 700", log);
        Assert.Equal(GapOutcome.Bidding, store.Gaps(1)[0].Outcome);
        Assert.Equal("t2", store.Gaps(1)[0].TradeId);
    }

    [Fact]
    public void AnAuctionWeWonIsBankedAndNotBoughtAgain()
    {
        var bidding = Gap(0) with { Outcome = GapOutcome.Bidding, TradeId = "t1", BidAmount = 800 };
        var (result, store, log) = Buy(Run(FulfilmentMode.Live, 100000), [bidding],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t2", 700)] },
            standing: [new TradeState("t1", "closed", "highest", 800)]);

        Assert.Empty(log.Where(entry => entry.StartsWith("search")));
        Assert.Equal(GapOutcome.Won, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void AStandingBidStillRunningLeavesTheRunWaitingWithoutASecondCard()
    {
        var bidding = Gap(0) with { Outcome = GapOutcome.Bidding, TradeId = "t1", BidAmount = 800 };
        var (result, _, log) = Buy(Run(FulfilmentMode.Live, 100000), [bidding],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t2", 700)] },
            standing: [new TradeState("t1", "active", "highest", 800)]);

        Assert.Empty(log.Where(entry => entry.StartsWith("bid")));
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void ABidWrittenDownThatCannotBeReadBackStopsTheRunBeforeAnySearch()
    {
        var attempting = Gap(0) with { Outcome = GapOutcome.Attempting, TradeId = "t1", BidAmount = 800 };
        var (result, store, log) = Buy(Run(FulfilmentMode.Live, 100000), [attempting, Gap(1)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t2", 700)] });

        Assert.Empty(log.Where(entry => entry.StartsWith("search")));
        Assert.Equal(FulfilmentState.Failed, result.State);
        Assert.Equal(GapOutcome.Unresolved, store.Gaps(1)[0].Outcome);
    }

    [Fact]
    public void ABidTheMarketRefusedStopsTheRun()
    {
        var (result, store, _) = Buy(Run(FulfilmentMode.Live, 100000), [Gap(0), Gap(1)],
            new Dictionary<int, IReadOnlyList<AuctionListing>>
            {
                [0] = [Listing("t1", 800)],
                [1] = [Listing("t2", 800)]
            },
            new Dictionary<string, BidOutcome> { ["t1"] = BidOutcome.Failed }, polls: [[Due("t1", 800)]]);

        Assert.Equal(FulfilmentState.Failed, result.State);
        Assert.Equal(GapOutcome.Unresolved, store.Gaps(1)[0].Outcome);
        Assert.Equal(GapOutcome.Pending, store.Gaps(1)[1].Outcome);
    }

    [Fact]
    public void ABidTheAgentNeverSendsBecauseThePriceMovedIsNotTreatedAsAmbiguous()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedMarket market = new(log,
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 800)] }) { Withhold = true };

        market.Polls.Enqueue([Due("t1", 800)]);

        var result = new GapBuyer(market, store).Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)]);

        Assert.Equal(GapOutcome.TooExpensive, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void TheAmountRecordedIsTheOneTheMarketActuallyTook()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedMarket market = new(log,
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 800)] }) { Actual = 650 };

        market.Polls.Enqueue([Due("t1", 800)]);

        new GapBuyer(market, store).Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)]);

        Assert.Equal(650, store.Gaps(1)[0].BidAmount);
        Assert.Contains("gap 0 Attempting 800", log);
    }

    [Fact]
    public void ABidOvertakenAsItLandedLeavesTheGapOutbid()
    {
        var (result, store, _) = Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 800)] },
            new Dictionary<string, BidOutcome> { ["t1"] = BidOutcome.Overtaken }, polls: [[Due("t1", 800)]]);

        Assert.Equal(GapOutcome.Outbid, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void ASecondRunOverTheSameApprovalBuysNothingTwice()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        Dictionary<int, IReadOnlyList<AuctionListing>> results = new()
        {
            [0] = [Listing("t1", 800)],
            [1] = [Listing("t1", 800)]
        };
        ScriptedMarket first = new(log, results);

        first.Polls.Enqueue([Due("t1", 800)]);

        GapBuyer buyer = new(first, store);

        buyer.Buy(Run(FulfilmentMode.Live, 100000), [Gap(0)]);

        ScriptedMarket second = new(log, results, null, [Ended("t1", 800, "highest")]);
        var result = new GapBuyer(second, store).Buy(Run(FulfilmentMode.Live, 100000), store.Gaps(1));

        Assert.Single(log.Where(entry => entry.StartsWith("bid")));
        Assert.Equal(FulfilmentState.Buying, result.State);
        Assert.Equal(800, store.Gaps(1)[0].FinalPrice);
    }
}
